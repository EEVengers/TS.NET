using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class AnyEdgeTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, ArmedRisingEdge, ArmedFallingEdge, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private bool validParameters;
    private sbyte triggerLevel;
    private sbyte upperArmLevel;
    private sbyte lowerArmLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public AnyEdgeTriggerI8(TriggerChannelParameters triggerChannelParameters, EdgeTriggerParameters parameters)
    {
        SetParameters(parameters, triggerChannelParameters.TriggerChannelVpp, triggerChannelParameters.TriggerChannelOffsetV);
        SetHorizontal(1000000, 0, 0);
    }

    private void SetParameters(EdgeTriggerParameters parameters, double triggerChannelVpp, double triggerChannelOffsetV)
    {
        validParameters = true;
        triggerState = TriggerState.Unarmed;
        triggerLevel = 0;
        upperArmLevel = 0;
        lowerArmLevel = 0;

        int hysteresisCount = TriggerUtility.HysteresisValue(AdcResolution.EightBit, parameters.HysteresisPercent);
        int levelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.LevelV, triggerChannelVpp, triggerChannelOffsetV);
        int upperArmCount = levelCount + hysteresisCount;
        int lowerArmCount = levelCount - hysteresisCount;

        if (levelCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            levelCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            upperArmCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            lowerArmCount < TriggerUtility.AdcMin(AdcResolution.EightBit))
        {
            validParameters = false;
        }

        if (validParameters)
        {
            triggerLevel = checked((sbyte)levelCount);
            upperArmLevel = checked((sbyte)upperArmCount);
            lowerArmLevel = checked((sbyte)lowerArmCount);
        }
    }

    // Parameters are in units of samples, not time.
    public void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff)
    {
        if (windowWidth < 1000)
            throw new ArgumentException($"windowWidth cannot be less than 1000");
        if (windowTriggerPosition > windowWidth - 1)
            windowTriggerPosition = windowWidth - 1;

        captureSamples = windowWidth - windowTriggerPosition;
        captureRemaining = 0;

        holdoffSamples = windowWidth - captureSamples + additionalHoldoff;
        holdoffRemaining = windowWidth - captureSamples;

        if (holdoffRemaining != 0)
            triggerState = TriggerState.InHoldoff;
        else
            triggerState = TriggerState.Unarmed;
    }

    public void Process(ReadOnlySpan<sbyte> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;

        if (!validParameters)
            return;

        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<sbyte>.Count;

        int i = 0;

        Vector256<sbyte> triggerLevelVector256 = Vector256.Create(triggerLevel);
        Vector256<sbyte> upperArmLevelVector256 = Vector256.Create(upperArmLevel);
        Vector256<sbyte> lowerArmLevelVector256 = Vector256.Create(lowerArmLevel);
        Vector128<sbyte> triggerLevelVector128 = Vector128.Create(triggerLevel);
        Vector128<sbyte> upperArmLevelVector128 = Vector128.Create(upperArmLevel);
        Vector128<sbyte> lowerArmLevelVector128 = Vector128.Create(lowerArmLevel);

        unsafe
        {
            fixed (sbyte* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        case TriggerState.Unarmed:
                            // The arming code has rising-edge-priority.
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < v256Length)
                                {
                                    var inputVector = Vector256.Load(samplesPtr + i);
                                    var lowerArmRegion = Vector256.LessThanOrEqual(inputVector, lowerArmLevelVector256);
                                    if (lowerArmRegion != Vector256<sbyte>.Zero)
                                        break;
                                    var upperArmRegion = Vector256.GreaterThanOrEqual(inputVector, upperArmLevelVector256);
                                    if (upperArmRegion != Vector256<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                    var lowerArmRegion1 = AdvSimd.CompareLessThanOrEqual(inputVector1, lowerArmLevelVector128);
                                    var lowerArmRegion2 = AdvSimd.CompareLessThanOrEqual(inputVector2, lowerArmLevelVector128);
                                    if (lowerArmRegion1 != Vector128<sbyte>.Zero || lowerArmRegion2 != Vector128<sbyte>.Zero)
                                        break;
                                    var upperArmRegion1 = AdvSimd.CompareGreaterThanOrEqual(inputVector1, upperArmLevelVector128);
                                    var upperArmRegion2 = AdvSimd.CompareGreaterThanOrEqual(inputVector2, upperArmLevelVector128);
                                    if (upperArmRegion1 != Vector128<sbyte>.Zero || upperArmRegion2 != Vector128<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] <= lowerArmLevel)
                                {
                                    triggerState = TriggerState.ArmedRisingEdge;
                                    results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                if (samplesPtr[i] >= upperArmLevel)
                                {
                                    triggerState = TriggerState.ArmedFallingEdge;
                                    results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.ArmedRisingEdge:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < v256Length)
                                {
                                    var inputVector = Vector256.Load(samplesPtr + i);
                                    var resultVector = Vector256.GreaterThan(inputVector, triggerLevelVector256);
                                    if (resultVector != Vector256<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                    var resultVector1 = AdvSimd.CompareGreaterThan(inputVector1, triggerLevelVector128);
                                    var resultVector2 = AdvSimd.CompareGreaterThan(inputVector2, triggerLevelVector128);
                                    if (resultVector1 != Vector128<sbyte>.Zero || resultVector2 != Vector128<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] > triggerLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    captureRemaining = captureSamples;
                                    results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.ArmedFallingEdge:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < v256Length)
                                {
                                    var inputVector = Vector256.Load(samplesPtr + i);
                                    var resultVector = Vector256.LessThan(inputVector, triggerLevelVector256);
                                    if (resultVector != Vector256<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                    var resultVector1 = AdvSimd.CompareLessThan(inputVector1, triggerLevelVector128);
                                    var resultVector2 = AdvSimd.CompareLessThan(inputVector2, triggerLevelVector128);
                                    if (resultVector1 != Vector128<sbyte>.Zero || resultVector2 != Vector128<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] < triggerLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    captureRemaining = captureSamples;
                                    results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.InCapture:
                            {
                                int remainingSamples = inputLength - i;
                                if (remainingSamples > captureRemaining)
                                {
                                    i += (int)captureRemaining;    // Cast is ok because remainingSamples (in the conditional expression) is uint
                                    captureRemaining = 0;
                                }
                                else
                                {
                                    captureRemaining -= remainingSamples;
                                    i = inputLength;    // Ends the state machine loop
                                }
                                if (captureRemaining == 0)
                                {
                                    results.CaptureEndIndices[results.CaptureEndCount++] = sampleStartIndex + (ulong)i;
                                    if (holdoffSamples > 0)
                                    {
                                        triggerState = TriggerState.InHoldoff;
                                        holdoffRemaining = holdoffSamples;
                                    }
                                    else
                                    {
                                        triggerState = TriggerState.Unarmed;
                                    }
                                }
                            }

                            break;
                        case TriggerState.InHoldoff:
                            {
                                int remainingSamples = inputLength - i;
                                if (remainingSamples > holdoffRemaining)
                                {
                                    i += (int)holdoffRemaining;    // Cast is ok because remainingSamples (in the conditional expression) is uint
                                    holdoffRemaining = 0;
                                }
                                else
                                {
                                    holdoffRemaining -= remainingSamples;
                                    i = inputLength;    // Ends the state machine loop
                                }
                                if (holdoffRemaining == 0)
                                {
                                    triggerState = TriggerState.Unarmed;
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}
