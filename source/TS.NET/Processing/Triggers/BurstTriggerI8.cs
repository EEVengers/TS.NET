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

    private ulong captureSamples;
    private ulong captureRemaining;

    private ulong holdoffSamples;
    private ulong holdoffRemaining;

    private ulong windowInRangePeriod;
    private ulong windowInRangePeriodRemaining;

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
        windowInRangePeriod = parameters.MinimumQuietPeriod;
        windowInRangePeriodRemaining = 0;
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
                            // Look for a period where the samples remain within the window for windowInRangePeriod length

                            if (windowInRangePeriodRemaining == 0)  // Assign variables if initial condition
                                windowInRangePeriodRemaining = windowInRangePeriod;

                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                // AVX2 fast path to scan through the quiet period then fall back to scalar near the end
                                while (i < simdLength && windowInRangePeriodRemaining > 32)
                                {
                                    var inputVector = Vector256.Load(samplesPtr + i);
                                    // The quiet window excludes the high/low level. e.g. if Low = -20 and High = 20, then values must be in -19 to 19 range.
                                    var gt = Vector256.GreaterThan(inputVector, windowLowLevelVector);
                                    var lt = Vector256.LessThan(inputVector, windowHighLevelVector);
                                    //var gtLowLimit = Avx2.MoveMask(gt) != 0;      // vpmovmskb
                                    //var ltHighLimit = Avx2.MoveMask(lt) != 0;
                                    var gtLowLimit = gt != Vector256<sbyte>.Zero;   // vptest  (better than vpmovmskb on most architectures)
                                    var ltHighLimit = lt != Vector256<sbyte>.Zero;
                                    if (gtLowLimit && ltHighLimit)
                                        windowInRangePeriodRemaining -= 32;
                                    else
                                        windowInRangePeriodRemaining = windowInRangePeriod;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] > windowLowLevel && samplesPtr[(int)i] < windowHighLevel)
                                    windowInRangePeriodRemaining--;
                                else
                                    windowInRangePeriodRemaining = windowInRangePeriod;
                                i++;

                                if (windowInRangePeriodRemaining == 0)
                                {
                                    triggerState = TriggerState.Armed;
                                    break;
                                }
                            }
                            break;
                        case TriggerState.Armed:
                            // To do: window trigger SIMD scan. Make a note of which window edge, for trigger interpolation calculation.
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < simdLength)
                                {
                                    var inputVector = Vector256.Load(samplesPtr + i);
                                    var lte = Vector256.LessThanOrEqual(inputVector, windowLowLevelVector);
                                    var gte = Vector256.GreaterThanOrEqual(inputVector, windowHighLevelVector);
                                    var lteLowLimit = lte != Vector256<sbyte>.Zero;
                                    var gteHighLimit = gte != Vector256<sbyte>.Zero;
                                    var conditionFound = lteLowLimit || gteHighLimit;
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] <= windowLowLevel || samplesPtr[(int)i] >= windowHighLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.InCapture:
                            {
                                if (captureRemaining == 0)  // Assign variables if initial condition
                                    captureRemaining = captureSamples;

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
