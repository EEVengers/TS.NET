﻿using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public class RisingEdgeTriggerI8 : ITriggerI8
{
    enum TriggerState { Unarmed, Armed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private sbyte triggerLevel;
    private sbyte armLevel;

    private ulong captureSamples;
    private ulong captureRemaining;

    private ulong holdoffSamples;
    private ulong holdoffRemaining;

    public RisingEdgeTriggerI8(EdgeTriggerParameters parameters)
    {
        SetParameters(parameters);
        SetHorizontal(1000000, 0, 0);
    }

    public void SetParameters(EdgeTriggerParameters parameters)
    {
        if (parameters.Level == sbyte.MinValue)
            parameters.Level += (sbyte)parameters.Hysteresis;  // Coerce so that the trigger arm level is sbyte.MinValue, ensuring a non-zero chance of seeing some waveforms
        if (parameters.Level == sbyte.MaxValue)
            parameters.Level -= 1;                  // Coerce as the trigger logic is GT, ensuring a non-zero chance of seeing some waveforms

        triggerState = TriggerState.Unarmed;
        triggerLevel = (sbyte)parameters.Level;
        armLevel = (sbyte)parameters.Level;
        armLevel -= (sbyte)parameters.Hysteresis;
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

        Vector256<sbyte> triggerLevelVector256 = Vector256.Create(triggerLevel);
        Vector256<sbyte> armLevelVector256 = Vector256.Create(armLevel);
        Vector128<sbyte> triggerLevelVector128 = Vector128.Create(triggerLevel);
        Vector128<sbyte> armLevelVector128 = Vector128.Create(armLevel);

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
                                    var resultVector = Avx2.CompareEqual(Avx2.Max(armLevelVector256, inputVector), armLevelVector256);
                                    var conditionFound = Avx2.MoveMask(resultVector) != 0;     // Quick way to do horizontal vector scan of byte[n] > 0
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < simdLength)
                                {
                                    var inputVector = AdvSimd.LoadVector128(samplesPtr + i);
                                    var resultVector = AdvSimd.CompareLessThanOrEqual(inputVector, armLevelVector128);
                                    var conditionFound = resultVector != Vector128<sbyte>.Zero;
                                    if (conditionFound)
                                        break;
                                    // ldr     q16, [x0, x1]
                                    // cmge    v16.16b, v9.16b, v16.16b
                                    // umaxp   v16.4s, v16.4s, v16.4s
                                    // umov    x1, v16.d[0]
                                    // cmp     x1, #0
                                    i += 16;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] <= armLevel)
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
                                    var resultVector = Avx2.CompareEqual(Avx2.Min(triggerLevelVector256, inputVector), triggerLevelVector256);
                                    var conditionFound = Avx2.MoveMask(resultVector) != 0;     // Quick way to do horizontal vector scan of byte[n] != 0
                                    if (conditionFound)
                                        break;
                                    i += 32;
                                }
                            }
                            else if (AdvSimd.Arm64.IsSupported)
                            {
                                while (i < simdLength)
                                {
                                    var inputVector = AdvSimd.LoadVector128(samplesPtr + i);
                                    var resultVector = AdvSimd.CompareGreaterThan(inputVector, triggerLevelVector128);
                                    var conditionFound = resultVector != Vector128<sbyte>.Zero;
                                    if (conditionFound)
                                        break;
                                    // ldr     q16, [x0, x1]
                                    // cmgt    v16.16b, v16.16b, v8.16b
                                    // umaxp   v16.4s, v16.4s, v16.4s
                                    // umov    x1, v16.d[0]
                                    // cmp     x1, #0
                                    i += 16;
                                }
                            }
                            while (i < inputLength)
                            {
                                if (samplesPtr[(int)i] > triggerLevel)
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