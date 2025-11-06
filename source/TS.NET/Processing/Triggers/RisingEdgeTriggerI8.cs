using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class RisingEdgeTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private sbyte triggerLevel;
    private sbyte armLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public RisingEdgeTriggerI8(EdgeTriggerParameters parameters, double triggerChannelVpp)
    {
        SetParameters(parameters, triggerChannelVpp);
        SetHorizontal(1000000, 0, 0);
    }

    public void SetParameters(EdgeTriggerParameters parameters, double triggerChannelVpp)
    {
        int hysteresisCount = TriggerUtility.HysteresisValue(AdcResolution.EightBit, parameters.HysteresisPercent);
        int levelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.LevelV, triggerChannelVpp);

        if (levelCount >= sbyte.MaxValue)
            levelCount = sbyte.MaxValue - 1;  // Coerce as the trigger logic is GT, ensuring a non-zero chance of seeing some waveforms             

        triggerState = TriggerState.Unarmed;
        triggerLevel = (sbyte)levelCount;     // Logic = GT

        if ((levelCount - hysteresisCount) < sbyte.MinValue)
        {
            armLevel = sbyte.MinValue;              // Logic = LTE
        }
        else
        {
            armLevel = (sbyte)(levelCount - hysteresisCount);
        }
    }

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

    public void Process(ReadOnlySpan<sbyte> input, ref EdgeTriggerResults results)
    {
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
                                    var conditionFound = Avx2.MoveMask(resultVector) != 0;     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (conditionFound)     // Alternatively, use BitOperations.TrailingZeroCount and add the offset
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + 16);
                                    var resultVector1 = AdvSimd.CompareLessThanOrEqual(inputVector1, armLevelVector128);
                                    var resultVector2 = AdvSimd.CompareLessThanOrEqual(inputVector2, armLevelVector128);
                                    var conditionFound = resultVector1 != Vector128<sbyte>.Zero;
                                    conditionFound |= resultVector2 != Vector128<sbyte>.Zero;
                                    if (conditionFound)
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
                                    results.ArmIndices[results.ArmCount++] = i;
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
                                    var conditionFound = Avx2.MoveMask(resultVector) != 0;     // Quick way to do horizontal vector scan of byte[n] != 0
                                    if (conditionFound)     // Alternatively, use BitOperations.TrailingZeroCount and add the offset
                                        break;
                                    i += Vector256<sbyte>.Count;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < v256Length)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + 16);
                                    var resultVector1 = AdvSimd.CompareGreaterThan(inputVector1, triggerLevelVector128);
                                    var resultVector2 = AdvSimd.CompareGreaterThan(inputVector2, triggerLevelVector128);
                                    var conditionFound = resultVector1 != Vector128<sbyte>.Zero;
                                    conditionFound |= resultVector2 != Vector128<sbyte>.Zero;
                                    if (conditionFound)
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
                                    results.TriggerIndices[results.TriggerCount++] = i;
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
                                    results.CaptureEndIndices[results.CaptureEndCount++] = i;
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