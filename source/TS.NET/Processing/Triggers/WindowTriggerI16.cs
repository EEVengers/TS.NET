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
    private short upperTriggerLevel;
    private short lowerTriggerLevel;
    private short upperArmLevel;
    private short lowerArmLevel;

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
        upperTriggerLevel = 0;
        lowerTriggerLevel = 0;
        upperArmLevel = 0;
        lowerArmLevel = 0;

        int upperTriggerLevelCount = TriggerUtility.LevelValue(adcResolution, parameters.UpperLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int lowerTriggerLevelCount = TriggerUtility.LevelValue(adcResolution, parameters.LowerLevelV, triggerChannelVpp, triggerChannelOffsetV);
        int hysteresisCount = TriggerUtility.HysteresisValue(adcResolution, parameters.HysteresisPercent);
        int upperArmCount = parameters.Direction == WindowDirection.Enter ? upperTriggerLevelCount + hysteresisCount : upperTriggerLevelCount - hysteresisCount;
        int lowerArmCount = parameters.Direction == WindowDirection.Enter ? lowerTriggerLevelCount - hysteresisCount : lowerTriggerLevelCount + hysteresisCount;

        if (upperTriggerLevelCount < TriggerUtility.AdcMin(adcResolution) ||
            upperTriggerLevelCount > TriggerUtility.AdcMax(adcResolution) ||
            lowerTriggerLevelCount < TriggerUtility.AdcMin(adcResolution) ||
            lowerTriggerLevelCount > TriggerUtility.AdcMax(adcResolution) ||
            upperArmCount > TriggerUtility.AdcMax(adcResolution) ||
            lowerArmCount < TriggerUtility.AdcMin(adcResolution) ||
            lowerArmCount >= upperArmCount ||
            lowerTriggerLevelCount >= upperTriggerLevelCount)
        {
            validParameters = false;
        }

        if (validParameters)
        {
            upperTriggerLevel = checked((short)upperTriggerLevelCount);
            lowerTriggerLevel = checked((short)lowerTriggerLevelCount);
            upperArmLevel = checked((short)upperArmCount);
            lowerArmLevel = checked((short)lowerArmCount);
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

    public void Process(ReadOnlySpan<short> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;

        if (!validParameters)
            return;

        int inputLength = input.Length;
        int v256Length = inputLength - Vector256<short>.Count;

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
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var outsideWindow = Vector256.GreaterThan(inputVector, upperArmLevelVector256) | Vector256.LessThan(inputVector, lowerArmLevelVector256);
                                            if (outsideWindow != Vector256<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var inWindow = Vector256.LessThan(inputVector, upperArmLevelVector256) & Vector256.GreaterThan(inputVector, lowerArmLevelVector256);
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
                                            var outsideWindow1 = AdvSimd.CompareGreaterThan(inputVector1, upperArmLevelVector128) | AdvSimd.CompareLessThan(inputVector1, lowerArmLevelVector128);
                                            var outsideWindow2 = AdvSimd.CompareGreaterThan(inputVector2, upperArmLevelVector128) | AdvSimd.CompareLessThan(inputVector2, lowerArmLevelVector128);
                                            if (outsideWindow1 != Vector128<short>.Zero || outsideWindow2 != Vector128<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector1 = AdvSimd.LoadVector128(samplesPtr + i);
                                            var inputVector2 = AdvSimd.LoadVector128(samplesPtr + i + Vector128<short>.Count);
                                            var inWindow1 = AdvSimd.CompareLessThan(inputVector1, upperArmLevelVector128) & AdvSimd.CompareGreaterThan(inputVector1, lowerArmLevelVector128);
                                            var inWindow2 = AdvSimd.CompareLessThan(inputVector2, upperArmLevelVector128) & AdvSimd.CompareGreaterThan(inputVector2, lowerArmLevelVector128);
                                            if (inWindow1 != Vector128<short>.Zero || inWindow2 != Vector128<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
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
                                            if (inWindow != Vector256<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
                                        }
                                        break;
                                    case WindowDirection.Exit:
                                        while (i < v256Length)
                                        {
                                            var inputVector = Vector256.Load(samplesPtr + i);
                                            var outsideWindow = Vector256.GreaterThanOrEqual(inputVector, upperTriggerLevelVector256) | Vector256.LessThanOrEqual(inputVector, lowerTriggerLevelVector256);
                                            if (outsideWindow != Vector256<short>.Zero)
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
                                            var inWindow1 = AdvSimd.CompareLessThanOrEqual(inputVector1, upperTriggerLevelVector128) & AdvSimd.CompareGreaterThanOrEqual(inputVector1, lowerTriggerLevelVector128);
                                            var inWindow2 = AdvSimd.CompareLessThanOrEqual(inputVector2, upperTriggerLevelVector128) & AdvSimd.CompareGreaterThanOrEqual(inputVector2, lowerTriggerLevelVector128);
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
                                            var outsideWindow1 = AdvSimd.CompareGreaterThanOrEqual(inputVector1, upperTriggerLevelVector128) | AdvSimd.CompareLessThanOrEqual(inputVector1, lowerTriggerLevelVector128);
                                            var outsideWindow2 = AdvSimd.CompareGreaterThanOrEqual(inputVector2, upperTriggerLevelVector128) | AdvSimd.CompareLessThanOrEqual(inputVector2, lowerTriggerLevelVector128);
                                            if (outsideWindow1 != Vector128<short>.Zero || outsideWindow2 != Vector128<short>.Zero)
                                                break;
                                            i += Vector256<short>.Count;
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