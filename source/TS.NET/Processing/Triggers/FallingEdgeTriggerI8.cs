using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class FallingEdgeTriggerI8 : IEdgeTriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;
    private sbyte triggerLevel;
    private sbyte armLevel;

    private ulong captureSamples;
    private ulong captureRemaining;

    private ulong holdoffSamples;
    private ulong holdoffRemaining;

    private Vector256<sbyte> triggerLevelVector;
    private Vector256<sbyte> armLevelVector;

    public FallingEdgeTriggerI8()
    {
        SetVertical(0, 5);
        SetHorizontal(1000000, 0, 0);
    }

    public void SetVertical(sbyte triggerLevel, byte triggerHysteresis)
    {
        if (triggerLevel == sbyte.MaxValue)
            triggerLevel -= (sbyte)triggerHysteresis;   // Coerce so that the trigger arm level is sbyte.MinValue, ensuring a non-zero chance of seeing some waveforms
        if (triggerLevel == sbyte.MinValue)
            triggerLevel += 1;                          // Coerce as the trigger logic is LT, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;

        this.triggerLevel = triggerLevel;
        armLevel = triggerLevel;
        armLevel += (sbyte)triggerHysteresis;

        triggerLevelVector = Vector256.Create(triggerLevel);
        armLevelVector = Vector256.Create(armLevel);
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

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                        case TriggerState.Unarmed:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < simdLength)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(armLevelVector, inputVector), armLevelVector);
                                    uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] >= armLevel)
                                {
                                    triggerState = TriggerState.Armed;
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
                                    var resultVector = Avx2.CompareEqual(Avx2.Max(triggerLevelVector, inputVector), triggerLevelVector);
                                    uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] < triggerLevel)
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