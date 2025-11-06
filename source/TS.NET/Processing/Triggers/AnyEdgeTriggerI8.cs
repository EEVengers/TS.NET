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

    public AnyEdgeTriggerI8(EdgeTriggerParameters parameters, double triggerChannelVpp)
    {
        SetParameters(parameters, triggerChannelVpp);
        SetHorizontal(1000000, 0, 0);
    }

    public void SetParameters(EdgeTriggerParameters parameters, double triggerChannelVpp)
    {
        int hysteresisCount = (int)((parameters.HysteresisPercent / 100.0) * 256);
        sbyte levelCount = (sbyte)Math.Clamp(Math.Round(parameters.LevelV * 128), sbyte.MinValue, sbyte.MaxValue);

        if (levelCount <= sbyte.MinValue)
            levelCount = sbyte.MinValue + 1;  // Coerce as the trigger logic is LT, ensuring a non-zero chance of seeing some waveforms
        if (levelCount >= sbyte.MaxValue)
            levelCount = sbyte.MaxValue - 1;  // Coerce as the trigger logic is GT, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;
        triggerLevel = (sbyte)levelCount;

        if ((levelCount + hysteresisCount) > sbyte.MaxValue)
        {
            upperArmLevel = sbyte.MaxValue;              // Logic = GTE
        }
        else
        {
            upperArmLevel = (sbyte)(levelCount + hysteresisCount);
        }

        if ((levelCount - hysteresisCount) < sbyte.MinValue)
        {
            lowerArmLevel = sbyte.MinValue;              // Logic = LTE
        }
        else
        {
            lowerArmLevel = (sbyte)(levelCount - hysteresisCount);
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
                                while (i < v256Length)
                                {
                                    uint resultCount = 0;
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var lowerArmRegion = Avx2.CompareEqual(Avx2.Max(lowerArmLevelVector, inputVector), lowerArmLevelVector);
                                    resultCount = (uint)Avx2.MoveMask(lowerArmRegion);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    var upperArmRegion = Avx2.CompareEqual(Avx2.Min(upperArmLevelVector, inputVector), upperArmLevelVector);
                                    resultCount = (uint)Avx2.MoveMask(upperArmRegion);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    i += Vector256<sbyte>.Count;
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
                                while (i < v256Length)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(triggerLevelVector, inputVector), triggerLevelVector);
                                    uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
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
                        case TriggerState.ArmedFallingEdge:
                            if (Avx2.IsSupported)       // Const after JIT/AOT
                            {
                                while (i < v256Length)
                                {
                                    var inputVector = Avx.LoadVector256(samplesPtr + i);
                                    var resultVector = Avx2.CompareEqual(Avx2.Max(triggerLevelVector, inputVector), triggerLevelVector);
                                    uint resultCount = (uint)Avx2.MoveMask(resultVector);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    i += Vector256<sbyte>.Count;
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
