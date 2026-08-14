using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class BurstTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, QuietComplete, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private bool validParameters;
    private BurstEdgeDirection triggerDirection;
    private sbyte triggerLevel;
    private sbyte armLevel;
    private sbyte quietUpperLevel;
    private sbyte quietLowerLevel;

    private long quietSamples;
    private long quietSamplesRemaining;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public BurstTriggerI8(TriggerChannelParameters triggerChannelParameters, BurstTriggerParameters parameters)
    {
        SetParameters(parameters, triggerChannelParameters.SampleRateHz, triggerChannelParameters.TriggerChannelVpp, triggerChannelParameters.TriggerChannelOffsetV);
        SetHorizontal(1000000, 500000, 0);
    }

    private void SetParameters(BurstTriggerParameters parameters, ulong sampleRateHz, double triggerChannelVpp, double triggerChannelOffsetV)
    {
        validParameters = true;
        triggerState = TriggerState.Unarmed;
        triggerLevel = 0;
        armLevel = 0;
        quietUpperLevel = 0;
        quietLowerLevel = 0;

        int hysteresisCount = TriggerUtility.HysteresisValue(AdcResolution.EightBit, parameters.HysteresisPercent);
        int levelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.LevelV, triggerChannelVpp, triggerChannelOffsetV);
        int quietUpperLevelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.QuietUpperLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int quietLowerLevelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.QuietLowerLevelV, triggerChannelVpp, triggerChannelOffsetV);
        triggerDirection = parameters.Direction;
        int armCount = triggerDirection switch
        {
            BurstEdgeDirection.Rising => levelCount - hysteresisCount,
            BurstEdgeDirection.Falling => levelCount + hysteresisCount,
            _ => throw new NotImplementedException()
        };

        if (levelCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            levelCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            armCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            armCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            quietUpperLevelCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            quietUpperLevelCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            quietLowerLevelCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            quietLowerLevelCount > TriggerUtility.AdcMax(AdcResolution.EightBit))
        {
            validParameters = false;
        }

        if (validParameters)
        {
            triggerLevel = checked((sbyte)levelCount);
            armLevel = checked((sbyte)armCount);
            quietUpperLevel = checked((sbyte)quietUpperLevelCount);
            quietLowerLevel = checked((sbyte)quietLowerLevelCount);
        }
        quietSamples = (long)Math.Ceiling(parameters.QuietTimeFs * (double)sampleRateHz / 1_000_000_000_000_000d);
        quietSamplesRemaining = 0;
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
        int simdBlock = 0;

        Vector256<sbyte> triggerLevelVector256 = Vector256.Create(triggerLevel);
        Vector256<sbyte> armLevelVector256 = Vector256.Create(armLevel);
        Vector256<sbyte> quietUpperLevelVector256 = Vector256.Create(quietUpperLevel);
        Vector256<sbyte> quietLowerLevelVector256 = Vector256.Create(quietLowerLevel);
        Vector128<sbyte> triggerLevelVector128 = Vector128.Create(triggerLevel);
        Vector128<sbyte> armLevelVector128 = Vector128.Create(armLevel);
        Vector128<sbyte> quietUpperLevelVector128 = Vector128.Create(quietUpperLevel);
        Vector128<sbyte> quietLowerLevelVector128 = Vector128.Create(quietLowerLevel);

        unsafe
        {
            fixed (sbyte* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        // Scan samples to ensure that they're within the quiet window.
                        case TriggerState.Unarmed:
                            // Look for a period where the samples remain within the quiet window for quietTime length.

                            if (quietSamplesRemaining == 0)  // Assign variables if initial condition
                                quietSamplesRemaining = quietSamples;

                            while (i < inputLength)
                            {
                                // The quiet window excludes the high/low level.
                                // e.g. if quietLowerLevel = -20 and quietUpperLevel = 20, then values must be in -19 to 19 range.
                                if (Avx2.IsSupported)
                                {
                                    while (i < v256Length && quietSamplesRemaining > Vector256<sbyte>.Count && simdBlock == 0)
                                    {
                                        var inputVector = Vector256.Load(samplesPtr + i);
                                        var gt = Vector256.GreaterThan(inputVector, quietLowerLevelVector256);
                                        var lt = Vector256.LessThan(inputVector, quietUpperLevelVector256);
                                        var fullyGt = gt == Vector256<sbyte>.AllBitsSet;   // vptest  (better than vpmovmskb on most architectures)
                                        var fullyLt = lt == Vector256<sbyte>.AllBitsSet;

                                        if (fullyGt && fullyLt)
                                        {
                                            quietSamplesRemaining -= Vector256<sbyte>.Count;
                                        }
                                        else
                                        {
                                            var partialGt = fullyGt ^ (gt != Vector256<sbyte>.Zero);
                                            var partialLt = fullyLt ^ (lt != Vector256<sbyte>.Zero);
                                            if (partialGt || partialLt)      // Window transition in SIMD block, fallback to scalar.
                                            {
                                                simdBlock = Vector256<sbyte>.Count;
                                                break;
                                            }
                                            else
                                            {
                                                quietSamplesRemaining = quietSamples;
                                            }
                                        }
                                        i += Vector256<sbyte>.Count;
                                    }
                                }
                                else if (AdvSimd.Arm64.IsSupported)
                                {
                                    while (i < v256Length && quietSamplesRemaining > Vector256<sbyte>.Count && simdBlock == 0)
                                    {
                                        var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                        var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                        var gt1 = AdvSimd.CompareGreaterThan(inputVector1, quietLowerLevelVector128);
                                        var lt1 = AdvSimd.CompareLessThan(inputVector1, quietUpperLevelVector128);
                                        var gt2 = AdvSimd.CompareGreaterThan(inputVector2, quietLowerLevelVector128);
                                        var lt2 = AdvSimd.CompareLessThan(inputVector2, quietUpperLevelVector128);
                                        var fullyGt = gt1 == Vector128<sbyte>.AllBitsSet && gt2 == Vector128<sbyte>.AllBitsSet;
                                        var fullyLt = lt1 == Vector128<sbyte>.AllBitsSet && lt2 == Vector128<sbyte>.AllBitsSet;

                                        if (fullyGt && fullyLt)
                                        {
                                            quietSamplesRemaining -= Vector256<sbyte>.Count;
                                        }
                                        else
                                        {
                                            var partialGt = fullyGt ^ (gt1 != Vector128<sbyte>.Zero || gt2 != Vector128<sbyte>.Zero);
                                            var partialLt = fullyLt ^ (lt1 != Vector128<sbyte>.Zero || lt2 != Vector128<sbyte>.Zero);
                                            if (partialGt || partialLt)
                                            {
                                                simdBlock = Vector256<sbyte>.Count;
                                                break;
                                            }
                                            else
                                            {
                                                quietSamplesRemaining = quietSamples;
                                            }
                                        }
                                        i += Vector256<sbyte>.Count;
                                    }
                                }
                                // Note, by this point SIMD logic should ensure quietTimeRemaining > 0.
                                if (samplesPtr[i] > quietLowerLevel && samplesPtr[i] < quietUpperLevel)
                                    quietSamplesRemaining--;
                                else
                                    quietSamplesRemaining = quietSamples;
                                i++;

                                if (simdBlock > 0)
                                    simdBlock--;

                                if (quietSamplesRemaining == 0)
                                {
                                    triggerState = TriggerState.QuietComplete;
                                    break;
                                }
                            }
                            break;
                        case TriggerState.QuietComplete:
                            switch (triggerDirection)
                            {
                                case BurstEdgeDirection.Rising:
                                    if (Avx2.IsSupported)       // Const after JIT/AOT
                                    {
                                        while (i < v256Length)
                                        {
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var resultVector = Vector256.LessThanOrEqual(inputVector, armLevelVector256);
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
                                case BurstEdgeDirection.Falling:
                                    if (Avx2.IsSupported)       // Const after JIT/AOT
                                    {
                                        while (i < v256Length)
                                        {
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var resultVector = Vector256.GreaterThanOrEqual(inputVector, armLevelVector256);
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
                                            var resultVector1 = AdvSimd.CompareGreaterThanOrEqual(inputVector1, armLevelVector128);
                                            var resultVector2 = AdvSimd.CompareGreaterThanOrEqual(inputVector2, armLevelVector128);
                                            if (resultVector1 != Vector128<sbyte>.Zero || resultVector2 != Vector128<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
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
                                default:
                                    throw new NotImplementedException();
                            }
                            break;

                        case TriggerState.Armed:
                            switch (triggerDirection)
                            {
                                case BurstEdgeDirection.Rising:
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
                                            results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                            break;
                                        }
                                        i++;
                                    }
                                    break;
                                case BurstEdgeDirection.Falling:
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
                                            results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                            break;
                                        }
                                        i++;
                                    }
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }
                            break;
                        case TriggerState.InCapture:
                            {
                                if (captureRemaining == 0)  // Assign variables if initial condition
                                    captureRemaining = captureSamples;

                                int remainingSamples = inputLength - i;
                                if (remainingSamples > captureRemaining)
                                {
                                    i += (int)captureRemaining;
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
                                if (holdoffRemaining == 0)  // Assign variables if initial condition
                                    holdoffRemaining = holdoffSamples;

                                int remainingSamples = inputLength - i;
                                if (remainingSamples > holdoffRemaining)
                                {
                                    i += (int)holdoffRemaining;
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
