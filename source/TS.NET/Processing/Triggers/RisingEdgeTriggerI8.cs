using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class RisingEdgeTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private bool validParameters;
    private sbyte triggerLevel;
    private sbyte armLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public RisingEdgeTriggerI8(TriggerChannelParameters triggerChannelParameters, EdgeTriggerParameters parameters)
    {
        SetParameters(parameters, triggerChannelParameters.TriggerChannelVpp, triggerChannelParameters.TriggerChannelOffsetV);
        SetHorizontal(1000000, 0, 0);
    }

    private void SetParameters(EdgeTriggerParameters parameters, double triggerChannelVpp, double triggerChannelOffsetV)
    {
        validParameters = true;
        triggerState = TriggerState.Unarmed;
        triggerLevel = 0;
        armLevel = 0;

        int hysteresisCount = TriggerUtility.HysteresisValue(AdcResolution.EightBit, parameters.HysteresisPercent);
        int levelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.LevelV, triggerChannelVpp, triggerChannelOffsetV);
        int armCount = levelCount - hysteresisCount;

        if (levelCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            armCount < TriggerUtility.AdcMin(AdcResolution.EightBit))
        {
            validParameters = false;
        }

        if(validParameters)
        {
            triggerLevel = checked((sbyte)levelCount);
            armLevel = checked((sbyte)armCount);
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
        if (!validParameters)
            return;

        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<sbyte>.Count;
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;

        int i = 0;

        Vector256<sbyte> triggerLevelVector256 = Vector256.Create(triggerLevel);
        Vector256<sbyte> armLevelVector256 = Vector256.Create(armLevel);
        Vector128<sbyte> triggerLevelVector128 = Vector128.Create(triggerLevel);
        Vector128<sbyte> armLevelVector128 = Vector128.Create(armLevel);

        unsafe
        {
            fixed (sbyte* samplesPtr = input)
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
                                    var resultVector = Avx2.CompareEqual(Avx2.Max(armLevelVector256, inputVector), armLevelVector256);
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
                                    var resultVector1 = AdvSimd.CompareLessThanOrEqual(inputVector1, armLevelVector128);
                                    var resultVector2 = AdvSimd.CompareLessThanOrEqual(inputVector2, armLevelVector128);
                                    if (resultVector1 != Vector128<sbyte>.Zero || resultVector2 != Vector128<sbyte>.Zero)
                                        break;
                                    i += Vector256<sbyte>.Count;

                                    // https://branchfree.org/2019/04/01/fitting-my-head-through-the-arm-holes-or-two-sequences-to-substitute-for-the-missing-pmovmskb-instruction-on-arm-neon/
                                    // var inputVector = AdvSimd.Arm64.Load4xVector128AndUnzip(samplesPtr + i);
                                    // var resultVector1 = AdvSimd.CompareLessThanOrEqual(inputVector.Value1, armLevelVector128);
                                    // var resultVector2 = AdvSimd.CompareLessThanOrEqual(inputVector.Value2, armLevelVector128);
                                    // var resultVector3 = AdvSimd.CompareLessThanOrEqual(inputVector.Value3, armLevelVector128);
                                    // var resultVector4 = AdvSimd.CompareLessThanOrEqual(inputVector.Value4, armLevelVector128);
                                    // var t0 = AdvSimd.ShiftRightAndInsert(resultVector2, resultVector1, 1);
                                    // var t1 = AdvSimd.ShiftRightAndInsert(resultVector4, resultVector3, 1);
                                    // var t2 = AdvSimd.ShiftRightAndInsert(t1,t0, 2);
                                    // var t3 = AdvSimd.ShiftRightAndInsert(t2,t2, 4);
                                    // var t4 = AdvSimd.ShiftRightLogicalNarrowingLower(t3.AsUInt16(), 4);
                                    // var result = t4.AsUInt64()[0];
                                    // if(result != 0)
                                    // {
                                    //     var offset = BitOperations.TrailingZeroCount(result);
                                    //     i += (uint)offset;
                                    //     break;
                                    // }
                                    // i += 64;

                                    // var inputVector = AdvSimd.Arm64.Load4xVector128(samplesPtr + i);
                                    // var resultVector1 = AdvSimd.CompareLessThanOrEqual(inputVector.Value1, armLevelVector128);
                                    // var resultVector2 = AdvSimd.CompareLessThanOrEqual(inputVector.Value2, armLevelVector128);
                                    // var resultVector3 = AdvSimd.CompareLessThanOrEqual(inputVector.Value3, armLevelVector128);
                                    // var resultVector4 = AdvSimd.CompareLessThanOrEqual(inputVector.Value4, armLevelVector128);
                                    // var conditionFound = resultVector1 != Vector128<sbyte>.Zero;
                                    // conditionFound |= resultVector2 != Vector128<sbyte>.Zero;
                                    // conditionFound |= resultVector3 != Vector128<sbyte>.Zero;
                                    // conditionFound |= resultVector4 != Vector128<sbyte>.Zero;
                                    // if (conditionFound)
                                    //     break;
                                    // i += 64;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] <= armLevel)
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
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(triggerLevelVector256, inputVector), triggerLevelVector256);
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