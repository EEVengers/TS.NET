using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class WindowTriggerI16 : ITriggerI16
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private bool validParameters;
    private WindowDirection direction;
    private short upperLevel;
    private short lowerLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public WindowTriggerI16(TriggerChannelParameters triggerChannelParameters, WindowTriggerParameters parameters)
    {
        SetParameters(parameters, AdcResolution.TwelveBit, triggerChannelParameters.TriggerChannelVpp, triggerChannelParameters.TriggerChannelOffsetV);
        SetHorizontal(1_000_000, 0, 0);
    }

    private void SetParameters(WindowTriggerParameters parameters, AdcResolution adcResolution, double triggerChannelVpp, double triggerChannelOffsetV)
    {
        validParameters = true;
        triggerState = TriggerState.Unarmed;
        upperLevel = 0;
        lowerLevel = 0;

        int upperLevelCount = TriggerUtility.LevelValue(adcResolution, parameters.UpperLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int lowerLevelCount = TriggerUtility.LevelValue(adcResolution, parameters.LowerLevelV, triggerChannelVpp, triggerChannelOffsetV);

        if (upperLevelCount < TriggerUtility.AdcMin(adcResolution) ||
            upperLevelCount > TriggerUtility.AdcMax(adcResolution) ||
            lowerLevelCount < TriggerUtility.AdcMin(adcResolution) ||
            lowerLevelCount > TriggerUtility.AdcMax(adcResolution))
        {
            validParameters = false;
        }

        if (validParameters)
        {
            upperLevel = checked((short)upperLevelCount);
            lowerLevel = checked((short)lowerLevelCount);
        }

        if (validParameters && lowerLevel >= upperLevel)
            throw new ArgumentException("Lower window level must be below the upper window level.");

        direction = parameters.Direction;
    }

    public void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff)
    {
        if (windowWidth < 1000)
            throw new ArgumentException("windowWidth cannot be less than 1000");
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
        if (!validParameters)
            return;

        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<short>.Count;
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;

        int i = 0;

        var upperLevelVector256 = Vector256.Create(upperLevel);
        var lowerLevelVector256 = Vector256.Create(lowerLevel);
        var upperLevelVector128 = Vector128.Create(upperLevel);
        var lowerLevelVector128 = Vector128.Create(lowerLevel);

        unsafe
        {
            fixed (short* samplesPtr = input)
            {
                while (i < inputLength)
                {
                    switch (triggerState)
                    {
                        case TriggerState.Unarmed:
                            if (Avx2.IsSupported)
                            {
                                switch (direction)
                                {
                                    case WindowDirection.Enter:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Avx.LoadVector256(samplesPtr + i);
                                            var inWindow = Avx2.And(Avx2.CompareGreaterThan(inputVector, lowerLevelVector256), Avx2.CompareGreaterThan(upperLevelVector256, inputVector));
                                            if (inWindow != Vector256<short>.AllBitsSet)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Avx.LoadVector256(samplesPtr + i);
                                            var inWindow = Avx2.And(Avx2.CompareGreaterThan(inputVector, lowerLevelVector256), Avx2.CompareGreaterThan(upperLevelVector256, inputVector));
                                            if (inWindow != Vector256<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                switch (direction)
                                {
                                    case WindowDirection.Enter:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<short>.AllBitsSet || inWindow2 != Vector128<short>.AllBitsSet)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<short>.Zero || inWindow2 != Vector128<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                }
                            }
                            while (i < inputLength)
                            {
                                bool isInWindow = samplesPtr[i] > lowerLevel && samplesPtr[i] < upperLevel;
                                if ((direction == WindowDirection.Enter && !isInWindow) || (direction == WindowDirection.Exit && isInWindow))
                                {
                                    triggerState = TriggerState.Armed;
                                    results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                    break;
                                }
                                i++;
                            }
                            break;
                        case TriggerState.Armed:
                            if (Avx2.IsSupported)
                            {
                                switch (direction)
                                {
                                    case WindowDirection.Enter:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Avx.LoadVector256(samplesPtr + i);
                                            var inWindow = Avx2.And(Avx2.CompareGreaterThan(inputVector, lowerLevelVector256), Avx2.CompareGreaterThan(upperLevelVector256, inputVector));
                                            if (inWindow != Vector256<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Avx.LoadVector256(samplesPtr + i);
                                            var inWindow = Avx2.And(Avx2.CompareGreaterThan(inputVector, lowerLevelVector256), Avx2.CompareGreaterThan(upperLevelVector256, inputVector));
                                            if (inWindow != Vector256<short>.AllBitsSet)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                switch (direction)
                                {
                                    case WindowDirection.Enter:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<short>.Zero || inWindow2 != Vector128<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<short>.AllBitsSet || inWindow2 != Vector128<short>.AllBitsSet)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                }
                            }
                            while (i < inputLength)
                            {
                                bool isInWindow = samplesPtr[i] > lowerLevel && samplesPtr[i] < upperLevel;
                                if ((direction == WindowDirection.Enter && isInWindow) || (direction == WindowDirection.Exit && !isInWindow))
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
                                int startIndex = i;
                                int remainingSamples = inputLength - i;
                                if (remainingSamples > captureRemaining)
                                {
                                    i += (int)captureRemaining;
                                    captureRemaining = 0;
                                }
                                else
                                {
                                    captureRemaining -= remainingSamples;
                                    i = inputLength;
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
                                int startIndex = i;
                                int remainingSamples = inputLength - i;
                                if (remainingSamples > holdoffRemaining)
                                {
                                    i += (int)holdoffRemaining;
                                    holdoffRemaining = 0;
                                }
                                else
                                {
                                    holdoffRemaining -= remainingSamples;
                                    i = inputLength;
                                }
                                if (holdoffRemaining == 0)
                                    triggerState = TriggerState.Unarmed;
                            }
                            break;
                    }
                }
            }
        }
    }

}