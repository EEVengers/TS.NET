using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class AnyEdgeTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, ArmedRisingEdge, ArmedFallingEdge, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private sbyte triggerLevel;
    private sbyte upperArmLevel;
    private sbyte lowerArmLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public AnyEdgeTriggerI8(EdgeTriggerParameters parameters)
    {
        SetParameters(parameters);
        SetHorizontal(1000000, 0, 0);
    }

    public void SetParameters(EdgeTriggerParameters parameters)
    {
        parameters.Hysteresis = Math.Abs(parameters.Hysteresis);

        if (parameters.Level <= sbyte.MinValue)
            parameters.Level = sbyte.MinValue + parameters.Hysteresis;  // Coerce so that the trigger arm level is sbyte.MinValue, ensuring a non-zero chance of seeing some waveforms
        if (parameters.Level >= sbyte.MaxValue)
            parameters.Level = sbyte.MaxValue - parameters.Hysteresis;  // Coerce so that the trigger arm level is sbyte.MaxValue, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;
        triggerLevel = (sbyte)parameters.Level;
        upperArmLevel = (sbyte)parameters.Level;
        upperArmLevel -= (sbyte)parameters.Hysteresis;
        lowerArmLevel = (sbyte)parameters.Level;
        lowerArmLevel += (sbyte)parameters.Hysteresis;
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

        Vector256<sbyte> triggerLevelVector = Vector256.Create(triggerLevel);
        Vector256<sbyte> upperArmLevelVector = Vector256.Create(upperArmLevel);
        Vector256<sbyte> lowerArmLevelVector = Vector256.Create(lowerArmLevel);

        unsafe
        {
            fixed (sbyte* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        case TriggerState.Unarmed:
                            // The arming code has rising-edge-priority.
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < simdLength)
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
                                    i += 32;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[i] <= lowerArmLevel)
                                {
                                    triggerState = TriggerState.ArmedRisingEdge;
                                    results.ArmIndices[results.ArmCount++] = i;
                                    break;
                                }
                                if (samplesPtr[i] >= upperArmLevel)
                                {
                                    triggerState = TriggerState.ArmedFallingEdge;
                                    results.ArmIndices[results.ArmCount++] = i;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.ArmedRisingEdge:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < simdLength)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(triggerLevelVector, inputVector), triggerLevelVector);
                                    uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    i += 32;
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
                        case TriggerState.ArmedFallingEdge:
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
                                if (samplesPtr[i] < triggerLevel)
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
