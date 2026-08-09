using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class FallingEdgeTriggerI16 : ITriggerI16
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private bool validParameters;
    private short triggerLevel;
    private short armLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public FallingEdgeTriggerI16(TriggerChannelParameters triggerChannelParameters, EdgeTriggerParameters parameters)
    {
        // Fixed 12-bit for now, may have 14-bit in future
        SetParameters(parameters, AdcResolution.TwelveBit, triggerChannelParameters.TriggerChannelVpp, triggerChannelParameters.TriggerChannelOffsetV);
        SetHorizontal(1000000, 0, 0);
    }

    private void SetParameters(EdgeTriggerParameters parameters, AdcResolution adcResolution, double triggerChannelVpp, double triggerChannelOffsetV)
    {
        validParameters = true;
        triggerState = TriggerState.Unarmed;
        triggerLevel = 0;
        armLevel = 0;

        int hysteresisCount = TriggerUtility.HysteresisValue(adcResolution, parameters.HysteresisPercent);
        int levelCount = TriggerUtility.LevelValue(adcResolution, parameters.LevelV, triggerChannelVpp, triggerChannelOffsetV);
        int armCount = levelCount + hysteresisCount;

        if (levelCount < TriggerUtility.AdcMin(adcResolution) ||
            armCount > TriggerUtility.AdcMax(adcResolution))
        {
            validParameters = false;
        }

        if (validParameters)
        {
            triggerLevel = checked((short)levelCount);
            armLevel = checked((short)armCount);
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

    public void Process(ReadOnlySpan<short> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        if (!validParameters)
            return;

        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<short>.Count;
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;

        int i = 0;

        Vector256<short> triggerLevelVector256 = Vector256.Create(triggerLevel);
        Vector256<short> armLevelVector256 = Vector256.Create(armLevel);
        Vector128<short> triggerLevelVector128 = Vector128.Create(triggerLevel);
        Vector128<short> armLevelVector128 = Vector128.Create(armLevel);

        unsafe
        {
            fixed (short* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        case TriggerState.Unarmed:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < v256Length)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(armLevelVector256, inputVector), armLevelVector256);
                                    // Convert 16-bit comparison results to 8-bit and extract mask
                                    var packedResult = Avx2.PackSignedSaturate(resultVector, Vector256<short>.Zero);
                                    var conditionFound = Avx2.MoveMask(packedResult) != 0;     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (conditionFound)
                                        break;
                                    i += Vector256<short>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                    var resultVector1 = AdvSimd.CompareGreaterThanOrEqual(inputVector1, armLevelVector128);
                                    var resultVector2 = AdvSimd.CompareGreaterThanOrEqual(inputVector2, armLevelVector128);
                                    if (resultVector1 != Vector128<short>.Zero || resultVector2 != Vector128<short>.Zero)
                                        break;
                                    i += Vector256<short>.Count;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] >= armLevel)
                                {
                                    triggerState = TriggerState.Armed;
                                    results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.Armed:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < v256Length)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Max(triggerLevelVector256, inputVector), triggerLevelVector256);
                                    // Convert 16-bit comparison results to 8-bit and extract mask
                                    var packedResult = Avx2.PackSignedSaturate(resultVector, Vector256<short>.Zero);
                                    var conditionFound = Avx2.MoveMask(packedResult) != 0;     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (conditionFound)
                                        break;
                                    i += Vector256<short>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                    var resultVector1 = AdvSimd.CompareLessThan(inputVector1, triggerLevelVector128);
                                    var resultVector2 = AdvSimd.CompareLessThan(inputVector2, triggerLevelVector128);
                                    if (resultVector1 != Vector128<short>.Zero || resultVector2 != Vector128<short>.Zero)
                                        break;
                                    i += Vector256<short>.Count;
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