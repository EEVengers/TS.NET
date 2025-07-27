using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class BurstTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private sbyte windowHighLevel;
    private sbyte windowLowLevel;
    Vector256<sbyte> windowHighLevelVector;
    Vector256<sbyte> windowLowLevelVector;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    private long windowInRangePeriod;
    private long windowInRangePeriodRemaining;

    public BurstTriggerI8(BurstTriggerParameters parameters)
    {
        SetParameters(parameters);
        SetHorizontal(1000000, 500000, 0);
    }

    public void SetParameters(BurstTriggerParameters parameters)
    {
        windowHighLevel = (sbyte)parameters.WindowHighLevel;
        windowLowLevel = (sbyte)parameters.WindowLowLevel;
        windowHighLevelVector = Vector256.Create(windowHighLevel);
        windowLowLevelVector = Vector256.Create(windowLowLevel);
        windowInRangePeriod = parameters.MinimumInRangePeriod;
        windowInRangePeriodRemaining = 0;
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
        int simdBlock = 0;

        unsafe
        {
            fixed (sbyte* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        // Scan samples to unsure that it's within window
                        case TriggerState.Unarmed:
                            // Look for a period where the samples remain within the window for windowInRangePeriod length

                            if (windowInRangePeriodRemaining == 0)  // Assign variables if initial condition
                                windowInRangePeriodRemaining = windowInRangePeriod;

                            while (i < inputLength)
                            {
                                // The in-range window excludes the high/low level.
                                // e.g. if windowLowLevel = -20 and windowHighLevel = 20, then values must be in -19 to 19 range.
                                if (Avx2.IsSupported)
                                {
                                    while (i < simdLength && windowInRangePeriodRemaining > 32 && simdBlock == 0)
                                    {
                                        var inputVector = Vector256.Load(samplesPtr + i);
                                        var gt = Vector256.GreaterThan(inputVector, windowLowLevelVector);
                                        var lt = Vector256.LessThan(inputVector, windowHighLevelVector);
                                        var fullyGt = gt == Vector256<sbyte>.AllBitsSet;   // vptest  (better than vpmovmskb on most architectures)
                                        var fullyLt = lt == Vector256<sbyte>.AllBitsSet;

                                        if (fullyGt && fullyLt)
                                        {
                                            windowInRangePeriodRemaining -= 32;
                                        }
                                        else
                                        {
                                            var partialGt = fullyGt ^ (gt != Vector256<sbyte>.Zero);
                                            var partialLt = fullyLt ^ (lt != Vector256<sbyte>.Zero);
                                            if (partialGt || partialLt)      // Window transition in SIMD block, fallback to scalar.
                                            {
                                                simdBlock = 32;
                                                break;
                                            }
                                            else
                                            {
                                                windowInRangePeriodRemaining = windowInRangePeriod;
                                            }
                                        }
                                        i += 32;
                                    }
                                }
                                // Note, by this point SIMD logic should ensure windowInRangePeriodRemaining > 0.
                                if (samplesPtr[i] > windowLowLevel && samplesPtr[i] < windowHighLevel)
                                    windowInRangePeriodRemaining--;
                                else
                                    windowInRangePeriodRemaining = windowInRangePeriod;
                                i++;

                                if (simdBlock > 0)
                                    simdBlock--;

                                if (windowInRangePeriodRemaining == 0)
                                {
                                    triggerState = TriggerState.Armed;
                                    results.ArmIndices[results.ArmCount++] = i;
                                    break;
                                }
                            }
                            break;
                        case TriggerState.Armed:
                            // To do: window trigger SIMD scan. Make a note of which window edge, for trigger interpolation calculation.
                            //if (Avx2.IsSupported)       // Const after JIT/AOT
                            //{
                            //    while (i < simdLength)
                            //    {
                            //        var inputVector = Vector256.Load(samplesPtr + i);
                            //        var lte = Vector256.LessThanOrEqual(inputVector, windowLowLevelVector);
                            //        var gte = Vector256.GreaterThanOrEqual(inputVector, windowHighLevelVector);
                            //        var lteLowLimit = lte != Vector256<sbyte>.Zero;
                            //        var gteHighLimit = gte != Vector256<sbyte>.Zero;
                            //        var conditionFound = lteLowLimit || gteHighLimit;
                            //        if (conditionFound)
                            //            break;
                            //        i += 32;
                            //    }
                            //}
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] <= windowLowLevel || samplesPtr[i] >= windowHighLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    results.TriggerIndices[results.TriggerCount++] = i;
                                    break;
                                }
                                i++;
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
                                    results.CaptureEndIndices[results.CaptureEndCount++] = i;
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
