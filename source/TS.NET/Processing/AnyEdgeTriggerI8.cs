using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class AnyEdgeTriggerI8 : IEdgeTriggerI8
{
    enum TriggerState { Unarmed, ArmedRisingEdge, ArmedFallingEdge, InCapture, InHoldoff }
    private TriggerState triggerState;
    private sbyte triggerLevel;
    private sbyte upperArmLevel;
    private sbyte lowerArmLevel;

    private ulong captureSamples;
    private ulong captureRemaining;

    private ulong holdoffSamples;
    private ulong holdoffRemaining;

    private Vector256<sbyte> triggerLevelVector;
    private Vector256<sbyte> upperArmLevelVector;
    private Vector256<sbyte> lowerArmLevelVector;

    public AnyEdgeTriggerI8(sbyte triggerLevel, byte triggerHysteresis, ulong windowWidth, ulong windowTriggerPosition, ulong additionalHoldoff)
    {
        triggerState = TriggerState.Unarmed;

        SetVertical(triggerLevel, triggerHysteresis);
        SetHorizontal(windowWidth, windowTriggerPosition, additionalHoldoff);
    }

    public void SetVertical(sbyte triggerLevel, byte triggerHysteresis)
    {
        if (triggerLevel == sbyte.MinValue)
            triggerLevel += (sbyte)triggerHysteresis;  // Coerce so that the trigger arm level is sbyte.MinValue, ensuring a non-zero chance of seeing some waveforms
        if (triggerLevel == sbyte.MaxValue)
            triggerLevel -= (sbyte)triggerHysteresis;  // Coerce so that the trigger arm level is sbyte.MaxValue, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;

        this.triggerLevel = triggerLevel;
        upperArmLevel = triggerLevel;
        upperArmLevel -= (sbyte)triggerHysteresis;
        lowerArmLevel = triggerLevel;
        lowerArmLevel += (sbyte)triggerHysteresis;

        triggerLevelVector = Vector256.Create(triggerLevel);
        upperArmLevelVector = Vector256.Create(upperArmLevel);
        lowerArmLevelVector = Vector256.Create(lowerArmLevel);
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
    public void ProcessSimd(ReadOnlySpan<sbyte> input, Span<uint> captureEndIndices, out uint captureEndCount)
    {
        uint inputLength = (uint)input.Length;
        uint simdLength = inputLength - 32;
        captureEndCount = 0;
        uint i = 0;

        captureEndIndices.Clear();
        unsafe
        {
            fixed (sbyte* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        case TriggerState.Unarmed:
                            // Process 32 bytes at a time.  For this simplified version, use SIMD to scan then fallback to serial processing
                            // The arming code has rising-edge-priority.
                            for (; i < simdLength; i += 32)
                            {
                                uint resultCount = 0;
                                var lowerArmRegion = Avx2.CompareEqual(Avx2.Max(lowerArmLevelVector, Avx.LoadVector256(samplesPtr + i)), lowerArmLevelVector);
                                resultCount = (uint)Avx2.MoveMask(lowerArmRegion);     // Quick way to do horizontal vector scan of byte[n] > 0
                                if (resultCount != 0)
                                    break;
                                var upperArmRegion = Avx2.CompareEqual(Avx2.Min(upperArmLevelVector, Avx.LoadVector256(samplesPtr + i)), upperArmLevelVector);
                                resultCount = (uint)Avx2.MoveMask(lowerArmRegion);     // Quick way to do horizontal vector scan of byte[n] > 0
                                if (resultCount != 0)
                                    break;
                            }
                            // Process 1 byte at a time
                            for (; i < inputLength; i++)
                            {
                                if (samplesPtr[(int)i] <= lowerArmLevel)
                                {
                                    triggerState = TriggerState.ArmedRisingEdge;
                                    break;
                                }
                                if (samplesPtr[(int)i] >= upperArmLevel)
                                {
                                    triggerState = TriggerState.ArmedFallingEdge;
                                    break;
                                }
                            }
                            break;
                        case TriggerState.ArmedRisingEdge:
                            // Process 32 bytes at a time. For this simplified version, use SIMD to scan then fallback to serial processing
                            for (; i < simdLength; i += 32)
                            {
                                var inputVector = Avx.LoadVector256(samplesPtr + i);
                                var resultVector = Avx2.CompareEqual(Avx2.Min(triggerLevelVector, inputVector), triggerLevelVector);
                                uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                if (resultCount != 0)
                                    break;
                            }
                            // Process 1 byte at a time
                            for (; i < inputLength; i++)
                            {
                                if (samplesPtr[(int)i] > triggerLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    captureRemaining = captureSamples;
                                    break;
                                }
                            }
                            break;
                        case TriggerState.ArmedFallingEdge:
                            // Process 32 bytes at a time. For this simplified version, use SIMD to scan then fallback to serial processing
                            for (; i < simdLength; i += 32)
                            {
                                var inputVector = Avx.LoadVector256(samplesPtr + i);
                                var resultVector = Avx2.CompareEqual(Avx2.Max(triggerLevelVector, inputVector), triggerLevelVector);
                                uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                if (resultCount != 0)
                                    break;
                            }
                            // Process 1 byte at a time
                            for (; i < inputLength; i++)
                            {
                                if (samplesPtr[(int)i] < triggerLevel)
                                {
                                    triggerState = TriggerState.InCapture;
                                    captureRemaining = captureSamples;
                                    break;
                                }
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
                                    captureEndIndices[(int)captureEndCount++] = i;
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
