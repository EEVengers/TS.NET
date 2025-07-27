using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class FallingEdgeTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private sbyte triggerLevel;
    private sbyte armLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public FallingEdgeTriggerI8(EdgeTriggerParameters parameters)
    {
        SetParameters(parameters);
        SetHorizontal(1000000, 0, 0);
    }

    public void SetParameters(EdgeTriggerParameters parameters)
    {
        parameters.Hysteresis = Math.Abs(parameters.Hysteresis);

        if (parameters.Level <= sbyte.MinValue)
            parameters.Level = sbyte.MinValue + 1;  // Coerce as the trigger logic is LT, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;
        triggerLevel = (sbyte)parameters.Level;     // Logic = LT

        if((parameters.Level + parameters.Hysteresis) > sbyte.MaxValue)
        {
            armLevel = sbyte.MaxValue;              // Logic = GTE
        }    
        else
        {
            armLevel = (sbyte)(parameters.Level + parameters.Hysteresis);
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
        int simdLength = inputLength - 32;
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
                                while (i < simdLength)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(armLevelVector256, inputVector), armLevelVector256);
                                    var conditionFound = Avx2.MoveMask(resultVector) != 0;     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < simdLength)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + 16);
                                    var resultVector1 = AdvSimd.CompareGreaterThanOrEqual(inputVector1, armLevelVector128);
                                    var resultVector2 = AdvSimd.CompareGreaterThanOrEqual(inputVector2, armLevelVector128);
                                    var conditionFound = resultVector1 != Vector128<sbyte>.Zero;
                                    conditionFound |= resultVector2 != Vector128<sbyte>.Zero;
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] >= armLevel)
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
                                while (i < simdLength)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Max(triggerLevelVector256, inputVector), triggerLevelVector256);
                                    var conditionFound = Avx2.MoveMask(resultVector) != 0;     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < simdLength)
                                {
                                    var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                    var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + 16);
                                    var resultVector1 = AdvSimd.CompareLessThan(inputVector1, triggerLevelVector128);
                                    var resultVector2 = AdvSimd.CompareLessThan(inputVector2, triggerLevelVector128);
                                    var conditionFound = resultVector1 != Vector128<sbyte>.Zero;
                                    conditionFound |= resultVector2 != Vector128<sbyte>.Zero;
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] < triggerLevel)
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