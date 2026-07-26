using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class WindowTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private WindowDirection direction;

    private sbyte upperLevel;
    private sbyte lowerLevel;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    public WindowTriggerI8(TriggerChannelParameters triggerChannelParameters, WindowTriggerParameters parameters)
    {
        SetParameters(parameters, triggerChannelParameters.TriggerChannelVpp, triggerChannelParameters.TriggerChannelOffsetV);
        SetHorizontal(1_000_000, 0, 0);
    }

    private void SetParameters(WindowTriggerParameters parameters, double triggerChannelVpp, double triggerChannelOffsetV)
    {
        int upperLevelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.UpperLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int lowerLevelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.LowerLevelV, triggerChannelVpp, triggerChannelOffsetV);
        upperLevel = (sbyte)Math.Clamp(upperLevelCount, sbyte.MinValue, sbyte.MaxValue);
        lowerLevel = (sbyte)Math.Clamp(lowerLevelCount, sbyte.MinValue, sbyte.MaxValue);

        if (lowerLevel >= upperLevel)
            throw new ArgumentException("Lower window level must be below the upper window level.");

        direction = parameters.Direction;
        triggerState = TriggerState.Unarmed;
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

    public void Process(ReadOnlySpan<sbyte> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<sbyte>.Count;
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
            fixed (sbyte* samplesPtr = input)
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
                                            if (inWindow != Vector256<sbyte>.AllBitsSet)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Avx.LoadVector256(samplesPtr + i);
                                            var inWindow = Avx2.And(Avx2.CompareGreaterThan(inputVector, lowerLevelVector256), Avx2.CompareGreaterThan(upperLevelVector256, inputVector));
                                            if (inWindow != Vector256<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
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
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<sbyte>.AllBitsSet || inWindow2 != Vector128<sbyte>.AllBitsSet)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<sbyte>.Zero || inWindow2 != Vector128<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                }
                            }
                            while (i < inputLength)
                            {
                                switch (direction)
                                {
                                    case WindowDirection.Enter:
                                        if (samplesPtr[i] > upperLevel || samplesPtr[i] < lowerLevel)
                                        {
                                            triggerState = TriggerState.Armed;
                                            results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        if (samplesPtr[i] < upperLevel && samplesPtr[i] > lowerLevel)
                                        {
                                            triggerState = TriggerState.Armed;
                                            results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                        }
                                        break;
                                }
                                if (triggerState == TriggerState.Armed)
                                    break;
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
                                            var inWindowOrBoundary = Avx2.Or(inWindow, Avx2.Or(Avx2.CompareEqual(inputVector, lowerLevelVector256), Avx2.CompareEqual(inputVector, upperLevelVector256)));
                                            if (inWindowOrBoundary != Vector256<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Avx.LoadVector256(samplesPtr + i);
                                            var inWindow = Avx2.And(Avx2.CompareGreaterThan(inputVector, lowerLevelVector256), Avx2.CompareGreaterThan(upperLevelVector256, inputVector));
                                            if (inWindow != Vector256<sbyte>.AllBitsSet)
                                                break;
                                            i += Vector256<sbyte>.Count;
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
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            var inWindowOrBoundary1 = AdvSimd.Or(inWindow1, AdvSimd.Or(AdvSimd.CompareEqual(inputVector1, lowerLevelVector128), AdvSimd.CompareEqual(inputVector1, upperLevelVector128)));
                                            var inWindowOrBoundary2 = AdvSimd.Or(inWindow2, AdvSimd.Or(AdvSimd.CompareEqual(inputVector2, lowerLevelVector128), AdvSimd.CompareEqual(inputVector2, upperLevelVector128)));
                                            if (inWindowOrBoundary1 != Vector128<sbyte>.Zero || inWindowOrBoundary2 != Vector128<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                            var inWindow1 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector1, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector1, upperLevelVector128));
                                            var inWindow2 = AdvSimd.And(AdvSimd.CompareGreaterThan(inputVector2, lowerLevelVector128), AdvSimd.CompareLessThan(inputVector2, upperLevelVector128));
                                            if (inWindow1 != Vector128<sbyte>.AllBitsSet || inWindow2 != Vector128<sbyte>.AllBitsSet)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                }
                            }
                            while (i < inputLength)
                            {
                                switch (direction)
                                {
                                    case WindowDirection.Enter:
                                        if (samplesPtr[i] <= upperLevel && samplesPtr[i] >= lowerLevel)
                                        {
                                            triggerState = TriggerState.InCapture;
                                            captureRemaining = captureSamples;
                                            results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        if (samplesPtr[i] >= upperLevel || samplesPtr[i] <= lowerLevel)
                                        {
                                            triggerState = TriggerState.InCapture;
                                            captureRemaining = captureSamples;
                                            results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                        }
                                        break;
                                }
                                if (triggerState == TriggerState.InCapture)
                                    break;
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