using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class WindowTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private bool validParameters;
    private WindowDirection direction;
    private sbyte upperTriggerLevel;
    private sbyte lowerTriggerLevel;
    private sbyte upperArmLevel;
    private sbyte lowerArmLevel;

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
        validParameters = true;
        triggerState = TriggerState.Unarmed;
        upperTriggerLevel = 0;
        lowerTriggerLevel = 0;
        upperArmLevel = 0;
        lowerArmLevel = 0;

        int upperTriggerLevelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.UpperLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int lowerTriggerLevelCount = TriggerUtility.LevelValue(AdcResolution.EightBit, parameters.LowerLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int hysteresisCount = TriggerUtility.HysteresisValue(AdcResolution.EightBit, parameters.HysteresisPercent);
        int upperArmCount = parameters.Direction == WindowDirection.Enter ? upperTriggerLevelCount + hysteresisCount : upperTriggerLevelCount - hysteresisCount;
        int lowerArmCount = parameters.Direction == WindowDirection.Enter ? lowerTriggerLevelCount - hysteresisCount : lowerTriggerLevelCount + hysteresisCount;
        if (upperTriggerLevelCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            upperTriggerLevelCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            lowerTriggerLevelCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            lowerTriggerLevelCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            upperArmCount > TriggerUtility.AdcMax(AdcResolution.EightBit) ||
            lowerArmCount < TriggerUtility.AdcMin(AdcResolution.EightBit) ||
            lowerArmCount >= upperArmCount ||
            lowerTriggerLevelCount >= upperTriggerLevelCount)
        {
            validParameters = false;
        }

        if (validParameters)
        {
            upperTriggerLevel = checked((sbyte)upperTriggerLevelCount);
            lowerTriggerLevel = checked((sbyte)lowerTriggerLevelCount);
            upperArmLevel = checked((sbyte)upperArmCount);
            lowerArmLevel = checked((sbyte)lowerArmCount);
        }

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

    public void Process(ReadOnlySpan<sbyte> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;

        if (!validParameters)
            return;

        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<sbyte>.Count;

        int i = 0;

        var upperTriggerLevelVector256 = Vector256.Create(upperTriggerLevel);
        var lowerTriggerLevelVector256 = Vector256.Create(lowerTriggerLevel);
        var upperArmLevelVector256 = Vector256.Create(upperArmLevel);
        var lowerArmLevelVector256 = Vector256.Create(lowerArmLevel);
        var upperTriggerLevelVector128 = Vector128.Create(upperTriggerLevel);
        var lowerTriggerLevelVector128 = Vector128.Create(lowerTriggerLevel);
        var upperArmLevelVector128 = Vector128.Create(upperArmLevel);
        var lowerArmLevelVector128 = Vector128.Create(lowerArmLevel);

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
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var outsideWindow = Vector256.GreaterThan(inputVector, upperArmLevelVector256) | Vector256.LessThan(inputVector, lowerArmLevelVector256);
                                            if (outsideWindow != Vector256<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var inWindow = Vector256.LessThan(inputVector, upperArmLevelVector256) & Vector256.GreaterThan(inputVector, lowerArmLevelVector256);
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
                                            var outsideWindow1 = AdvSimd.CompareGreaterThan(inputVector1, upperArmLevelVector128) | AdvSimd.CompareLessThan(inputVector1, lowerArmLevelVector128);
                                            var outsideWindow2 = AdvSimd.CompareGreaterThan(inputVector2, upperArmLevelVector128) | AdvSimd.CompareLessThan(inputVector2, lowerArmLevelVector128);
                                            if (outsideWindow1 != Vector128<sbyte>.Zero || outsideWindow2 != Vector128<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                            var inWindow1 = AdvSimd.CompareLessThan(inputVector1, upperArmLevelVector128) & AdvSimd.CompareGreaterThan(inputVector1, lowerArmLevelVector128);
                                            var inWindow2 = AdvSimd.CompareLessThan(inputVector2, upperArmLevelVector128) & AdvSimd.CompareGreaterThan(inputVector2, lowerArmLevelVector128);
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
                                        if (samplesPtr[i] > upperArmLevel || samplesPtr[i] < lowerArmLevel)
                                        {
                                            triggerState = TriggerState.Armed;
                                            results.ArmIndices[results.ArmCount++] = sampleStartIndex + (ulong)i;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        if (samplesPtr[i] < upperArmLevel && samplesPtr[i] > lowerArmLevel)
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
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var inWindow = Vector256.LessThanOrEqual(inputVector, upperTriggerLevelVector256) & Vector256.GreaterThanOrEqual(inputVector, lowerTriggerLevelVector256);
                                            if (inWindow != Vector256<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var outsideWindow = Vector256.GreaterThanOrEqual(inputVector, upperTriggerLevelVector256) | Vector256.LessThanOrEqual(inputVector, lowerTriggerLevelVector256);
                                            if (outsideWindow != Vector256<sbyte>.Zero)
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
                                            var inWindow1 = AdvSimd.CompareLessThanOrEqual(inputVector1, upperTriggerLevelVector128) & AdvSimd.CompareGreaterThanOrEqual(inputVector1, lowerTriggerLevelVector128);
                                            var inWindow2 = AdvSimd.CompareLessThanOrEqual(inputVector2, upperTriggerLevelVector128) & AdvSimd.CompareGreaterThanOrEqual(inputVector2, lowerTriggerLevelVector128);
                                            if (inWindow1 != Vector128<sbyte>.Zero || inWindow2 != Vector128<sbyte>.Zero)
                                                break;
                                            i += Vector256<sbyte>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<sbyte>.Count);
                                            var inWindow1 = AdvSimd.CompareGreaterThanOrEqual(inputVector1, upperTriggerLevelVector128) | AdvSimd.CompareLessThanOrEqual(inputVector1, lowerTriggerLevelVector128);
                                            var inWindow2 = AdvSimd.CompareGreaterThanOrEqual(inputVector2, upperTriggerLevelVector128) | AdvSimd.CompareLessThanOrEqual(inputVector2, lowerTriggerLevelVector128);
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
                                        if (samplesPtr[i] <= upperTriggerLevel && samplesPtr[i] >= lowerTriggerLevel)
                                        {
                                            triggerState = TriggerState.InCapture;
                                            captureRemaining = captureSamples;
                                            results.TriggerIndices[results.TriggerCount++] = sampleStartIndex + (ulong)i;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        if (samplesPtr[i] >= upperTriggerLevel || samplesPtr[i] <= lowerTriggerLevel)
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