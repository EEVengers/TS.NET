using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class BurstTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private sbyte windowHighLevel;
    private sbyte windowLowLevel;

    private ulong captureSamples;
    private ulong captureRemaining;

    private ulong holdoffSamples;
    private ulong holdoffRemaining;

    private ulong quietPeriod;
    private ulong quietPeriodRemaining;

    public BurstTriggerI8(BurstTriggerParameters parameters)
    {
        SetParameters(parameters);
        SetHorizontal(1000000, 500000, 0);
    }

    public void SetParameters(BurstTriggerParameters parameters)
    {
        windowHighLevel = (sbyte)parameters.WindowHighLevel;
        windowLowLevel = (sbyte)parameters.WindowLowLevel;
        quietPeriod = parameters.MinimumQuietPeriod;
    }

    public void SetHorizontal(ulong windowWidth, ulong windowTriggerPosition, ulong additionalHoldoff)
    {
        if (windowWidth < 1000)
            throw new ArgumentException($"windowWidth cannot be less than 1000");
        if (windowTriggerPosition > (windowWidth - 1))
            windowTriggerPosition = windowWidth - 1;

        triggerState = TriggerState.Unarmed;

        captureSamples = windowWidth - windowTriggerPosition;
        captureRemaining = 0;

        holdoffSamples = windowWidth - captureSamples + additionalHoldoff;
        holdoffRemaining = 0;
    }

    public void Process(ReadOnlySpan<sbyte> input, Span<uint> windowEndIndices, out uint windowEndCount)
    {
        uint inputLength = (uint)input.Length;
        uint simdLength = inputLength - 32;
        windowEndCount = 0;
        uint i = 0;

        Vector256<sbyte> unarmedHighLevelVector = Vector256.Create(windowHighLevel);
        Vector256<sbyte> unarmedLowLevelVector = Vector256.Create(windowLowLevel);

        windowEndIndices.Clear();
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
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < simdLength && quietPeriodRemaining > 32)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var greaterThanLowerLimit = Avx2.CompareGreaterThan(inputVector, unarmedLowLevelVector).AsByte();    // 0xFFFF....
                                    var lowerThanHighLimit = Avx2.CompareGreaterThan(unarmedHighLevelVector, inputVector).AsByte();      // 0xFFFF

                                    var gtLowLimit = (uint)Avx2.MoveMask(greaterThanLowerLimit) != 0;
                                    var ltHighLimit = (uint)Avx2.MoveMask(lowerThanHighLimit) != 0;
                                    if (gtLowLimit && ltHighLimit)
                                        quietPeriodRemaining -= 32;
                                    else
                                        quietPeriodRemaining = quietPeriod;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] < windowLowLevel || samplesPtr[(int)i] > windowHighLevel)
                                    quietPeriodRemaining = quietPeriod;
                                else
                                    quietPeriodRemaining--;
                                i++;

                                if (quietPeriodRemaining == 0)
                                {
                                    triggerState = TriggerState.Armed;
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
                            //        var inputVector = Avx.LoadVector256(samplesPtr + i);
                            //        var resultVector = Avx2.CompareEqual(Avx2.Min(triggerLevelVector, inputVector), triggerLevelVector);
                            //        uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                            //        if (resultCount != 0)
                            //            break;
                            //        i += 32;
                            //    }
                            //}
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] < windowLowLevel || samplesPtr[(int)i] > windowHighLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    captureRemaining = captureSamples;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.InCapture:
                            {
                                uint remainingSamples = inputLength - i;
                                if (remainingSamples > captureRemaining)
                                {
                                    i += (uint)captureRemaining;    // Cast is ok because remainingSamples (in the conditional expression) is uint
                                    captureRemaining = 0;
                                }
                                else
                                {
                                    captureRemaining -= remainingSamples;
                                    i = inputLength;    // Ends the state machine loop
                                }
                                if (captureRemaining == 0)
                                {
                                    windowEndIndices[(int)windowEndCount++] = i;
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
                                uint remainingSamples = inputLength - i;
                                if (remainingSamples > holdoffRemaining)
                                {
                                    i += (uint)holdoffRemaining;    // Cast is ok because remainingSamples (in the conditional expression) is uint
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
