using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class AnyEdgeTriggerI16 : ITriggerI16
{
    enum TriggerState { Unarmed, ArmedRisingEdge, ArmedFallingEdge, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private short triggerLevel;
    private short upperArmLevel;
    private short lowerArmLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public AnyEdgeTriggerI16(EdgeTriggerParameters parameters, AdcResolution resolution, double triggerChannelVpp)
    {
        SetParameters(parameters, resolution, triggerChannelVpp);
        SetHorizontal(1000000, 0, 0);
    }

    private void SetParameters(EdgeTriggerParameters parameters, AdcResolution adcResolution, double triggerChannelVpp)
    {
        int hysteresisCount = TriggerUtility.HysteresisValue(adcResolution, parameters.HysteresisPercent);
        int levelCount = TriggerUtility.LevelValue(adcResolution, parameters.LevelV, triggerChannelVpp);

        if (levelCount <= TriggerUtility.AdcMin(adcResolution))
            levelCount = TriggerUtility.AdcMin(adcResolution) + 1;  // Coerce as the trigger logic is LT, ensuring a non-zero chance of seeing some waveforms
        if (levelCount >= TriggerUtility.AdcMax(adcResolution))
            levelCount = TriggerUtility.AdcMax(adcResolution) - 1;  // Coerce as the trigger logic is GT, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;
        triggerLevel = (short)levelCount;

        if ((levelCount + hysteresisCount) > TriggerUtility.AdcMax(adcResolution))
        {
            upperArmLevel = (short)TriggerUtility.AdcMax(adcResolution);    // Logic = GTE
        }
        else
        {
            upperArmLevel = (short)(levelCount + hysteresisCount);
        }

        if ((levelCount - hysteresisCount) < TriggerUtility.AdcMin(adcResolution))
        {
            lowerArmLevel = (short)TriggerUtility.AdcMin(adcResolution);    // Logic = LTE
        }
        else
        {
            lowerArmLevel = (short)(levelCount - hysteresisCount);
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

    public void Process(ReadOnlySpan<short> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<short>.Count;
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;
        int i = 0;

        Vector256<short> triggerLevelVector = Vector256.Create(triggerLevel);
        Vector256<short> upperArmLevelVector = Vector256.Create(upperArmLevel);
        Vector256<short> lowerArmLevelVector = Vector256.Create(lowerArmLevel);

        unsafe
        {
            fixed (short* samplesPtr = input)
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
                                    // Convert 16-bit comparison results to 8-bit and extract mask
                                    var packedResult = Avx2.PackSignedSaturate(lowerArmRegion, Vector256<short>.Zero);
                                    resultCount = (uint)Avx2.MoveMask(packedResult);     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (resultCount != 0)
                                        break;
                                    var upperArmRegion = Avx2.CompareEqual(Avx2.Min(upperArmLevelVector, inputVector), upperArmLevelVector);
                                    // Convert 16-bit comparison results to 8-bit and extract mask
                                    packedResult = Avx2.PackSignedSaturate(upperArmRegion, Vector256<short>.Zero);
                                    resultCount = (uint)Avx2.MoveMask(packedResult);     // Quick way to do horizontal vector scan of byte[n] > 0
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
                                    results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                if (samplesPtr[i] >= upperArmLevel)
                                {
                                    triggerState = TriggerState.ArmedFallingEdge;
                                    results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
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
                                    // Convert 16-bit comparison results to 8-bit and extract mask
                                    var packedResult = Avx2.PackSignedSaturate(resultVector, Vector256<short>.Zero);
                                    uint resultCount = (uint)Avx2.MoveMask(packedResult);     // Quick way to do horizontal vector scan of byte[n] > 0
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
                                    results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
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
                                    // Convert 16-bit comparison results to 8-bit and extract mask
                                    var packedResult = Avx2.PackSignedSaturate(resultVector, Vector256<short>.Zero);
                                    uint resultCount = (uint)Avx2.MoveMask(packedResult);     // Quick way to do horizontal vector scan of byte[n] > 0
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
                                    results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
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
                                    results.CaptureEndIndices[results.CaptureEndCount++] = sampleStartIndex + (ulong)i;
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
