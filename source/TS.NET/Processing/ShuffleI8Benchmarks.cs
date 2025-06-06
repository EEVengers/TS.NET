﻿using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET
{
    public static class ShuffleI8Benchmarks
    {
        // For benchmarking

        // This is the baseline 2-channel run length 1 algorithm for comparison purposes
        public static void TwoChannelsRunLength1(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException($"Length of samples ({input.Length}) is not multiple of 32");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int loopIterations = input.Length / 32;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15, 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);
            Vector256<ulong> storeCh1Mask = Vector256.Create(ulong.MaxValue, ulong.MaxValue, 0, 0);
            Vector256<ulong> storeCh2Mask = Vector256.Create(0, 0, ulong.MaxValue, ulong.MaxValue);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 2;
            int ch2Offset = channelBlockSize - 2;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    for (int i = 0; i < loopIterations; i++)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask); // shuffled1 = <1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2>
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 16843009, 16843009, 33686018, 33686018, 33686018, 33686018>
                        var permuted1_64 = permuted1.AsUInt64();
                        Avx2.MaskStore(outputPtr, storeCh1Mask, permuted1_64);
                        Avx2.MaskStore(outputPtr + ch2Offset, storeCh2Mask, permuted1_64);
                        inputPtr += 32;
                        outputPtr += 2;
                    }
                }
            }
        }

        public static void TwoChannelsRunLength1VariantA(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException($"Length of samples ({input.Length}) is not multiple of 32");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            Vector256<sbyte> shuffleMask = Vector256.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15, 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 2;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask); // shuffled1 = <1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2>
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 16843009, 16843009, 33686018, 33686018, 33686018, 33686018>
                        var permuted1_64 = permuted1.AsUInt64();
                        outputPtr[0] = permuted1_64[0];
                        outputPtr[1] = permuted1_64[1];
                        outputPtr[0 + channelBlockSize] = permuted1_64[2];
                        outputPtr[1 + channelBlockSize] = permuted1_64[3];
                        inputPtr += 32;
                        outputPtr += 2;
                    }
                }
            }
        }

        public static void TwoChannelsNoSimd(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 2 != 0)
                throw new ArgumentException($"Input length must be multiple of 2");

            int channelBlockSizeBytes = output.Length / 2;
            int ch2Offset8b = channelBlockSizeBytes;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    sbyte* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        outputPtr[0] = inputPtr[0];
                        outputPtr[0 + ch2Offset8b] = inputPtr[1];
                        inputPtr += 2;
                        outputPtr++;
                    }
                }
            }
        }

        // This is the baseline 4-channel run length 1 algorithm for comparison purposes
        public static void FourChannelsRunLength1(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException($"Input length must be multiple of 32");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
            Vector256<ulong> storeCh1Mask = Vector256.Create(ulong.MaxValue, 0, 0, 0);
            Vector256<ulong> storeCh2Mask = Vector256.Create(0, ulong.MaxValue, 0, 0);
            Vector256<ulong> storeCh3Mask = Vector256.Create(0, 0, ulong.MaxValue, 0);
            Vector256<ulong> storeCh4Mask = Vector256.Create(0, 0, 0, ulong.MaxValue);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 4;
            int ch2Offset = channelBlockSize - 1;
            int ch3Offset = (channelBlockSize * 2) - 2;
            int ch4Offset = (channelBlockSize * 3) - 3;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        Vector256<ulong> shuffledVector = Avx2.PermuteVar8x32(Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask).AsInt32(), permuteMask).AsUInt64();
                        Avx2.MaskStore(outputPtr, storeCh1Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch2Offset, storeCh2Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch3Offset, storeCh3Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch4Offset, storeCh4Mask, shuffledVector);
                        inputPtr += 32;
                        outputPtr++;
                    }
                }
            }
        }

        public static void FourChannelsRunLength1VariantA(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 128 != 0)
                throw new ArgumentException($"Input length must be multiple of 128");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
            Vector256<sbyte> blendMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<ulong> storeCh1Mask = Vector256.Create(ulong.MaxValue, 0, 0, 0);
            Vector256<ulong> storeCh2Mask = Vector256.Create(0, ulong.MaxValue, 0, 0);
            Vector256<ulong> storeCh3Mask = Vector256.Create(0, 0, ulong.MaxValue, 0);
            Vector256<ulong> storeCh4Mask = Vector256.Create(0, 0, 0, ulong.MaxValue);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask);
                        // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);
                        // permuted1 = <16843009, 16843009, 33686018, 33686018, 50529027, 50529027, 67372036, 67372036>
                        var permuted1_64 = permuted1.AsUInt64();
                        outputPtr[0] = permuted1_64[0];
                        outputPtr[0 + ch2Offset] = permuted1_64[1];
                        outputPtr[0 + ch3Offset] = permuted1_64[2];
                        outputPtr[0 + ch4Offset] = permuted1_64[3];
                        inputPtr += 32;
                        outputPtr += 1;
                    }
                }
            }
        }

        public static void FourChannelsRunLength1VariantB(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 64 != 0)
                throw new ArgumentException($"Input length must be multiple of 64");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask); // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                        var shuffled2 = Avx2.Shuffle(Avx.LoadVector256(inputPtr + 32), shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 33686018, 33686018, 50529027, 50529027, 67372036, 67372036>
                        var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                        var permuted1_64 = permuted1.AsUInt64();
                        var permuted2_64 = permuted1.AsUInt64();

                        outputPtr[0] = permuted1_64[0];
                        outputPtr[1] = permuted2_64[0];
                        outputPtr[0 + ch2Offset] = permuted1_64[1];
                        outputPtr[1 + ch2Offset] = permuted2_64[1];
                        outputPtr[0 + ch3Offset] = permuted1_64[2];
                        outputPtr[1 + ch3Offset] = permuted2_64[2];
                        outputPtr[0 + ch4Offset] = permuted1_64[3];
                        outputPtr[1 + ch4Offset] = permuted2_64[3];

                        inputPtr += 64;
                        outputPtr += 2;
                    }
                }
            }
        }

        public static void FourChannelsRunLength1VariantC(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 128 != 0)
                throw new ArgumentException($"Input length must be multiple of 128");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
            Vector256<sbyte> blendMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<ulong> storeCh1Mask = Vector256.Create(ulong.MaxValue, 0, 0, 0);
            Vector256<ulong> storeCh2Mask = Vector256.Create(0, ulong.MaxValue, 0, 0);
            Vector256<ulong> storeCh3Mask = Vector256.Create(0, 0, ulong.MaxValue, 0);
            Vector256<ulong> storeCh4Mask = Vector256.Create(0, 0, 0, ulong.MaxValue);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask);
                        // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                        var shuffled2 = Avx2.Shuffle(Avx.LoadVector256(inputPtr + 32), shuffleMask);
                        var shuffled3 = Avx2.Shuffle(Avx.LoadVector256(inputPtr + 64), shuffleMask);
                        var shuffled4 = Avx2.Shuffle(Avx.LoadVector256(inputPtr + 96), shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);
                        // permuted1 = <16843009, 16843009, 33686018, 33686018, 50529027, 50529027, 67372036, 67372036>
                        var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                        var permuted3 = Avx2.PermuteVar8x32(shuffled3.AsInt32(), permuteMask);
                        var permuted4 = Avx2.PermuteVar8x32(shuffled4.AsInt32(), permuteMask);
                        var permuted1_64 = permuted1.AsUInt64();
                        var permuted2_64 = permuted1.AsUInt64();
                        var permuted3_64 = permuted1.AsUInt64();
                        var permuted4_64 = permuted1.AsUInt64();

                        outputPtr[0] = permuted1_64[0];
                        outputPtr[1] = permuted2_64[0];
                        outputPtr[2] = permuted3_64[0];
                        outputPtr[3] = permuted4_64[0];

                        outputPtr[0 + ch2Offset] = permuted1_64[1];
                        outputPtr[1 + ch2Offset] = permuted2_64[1];
                        outputPtr[2 + ch2Offset] = permuted3_64[1];
                        outputPtr[3 + ch2Offset] = permuted4_64[1];

                        outputPtr[0 + ch3Offset] = permuted1_64[2];
                        outputPtr[1 + ch3Offset] = permuted2_64[2];
                        outputPtr[2 + ch3Offset] = permuted3_64[2];
                        outputPtr[3 + ch3Offset] = permuted4_64[2];

                        outputPtr[0 + ch4Offset] = permuted1_64[3];
                        outputPtr[1 + ch4Offset] = permuted2_64[3];
                        outputPtr[2 + ch4Offset] = permuted3_64[3];
                        outputPtr[3 + ch4Offset] = permuted4_64[3];
                        inputPtr += 128;
                        outputPtr += 4;
                    }
                }
            }
        }

        public static void FourChannelsRunLength1VariantD(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int channelBlockSizeBytes = output.Length / 4;

            if (Avx2.IsSupported)       // Const after JIT/AOT
            {
                if (input.Length % 64 != 0)
                    throw new ArgumentException($"Input length must be multiple of 64");

                int ch2Offset64b = channelBlockSizeBytes / 8;
                int ch3Offset64b = (channelBlockSizeBytes * 2) / 8;
                int ch4Offset64b = (channelBlockSizeBytes * 3) / 8;
                Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
                Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
                unsafe
                {
                    fixed (sbyte* inputP = input)
                    fixed (sbyte* outputP = output)
                    {
                        sbyte* inputPtr = inputP;
                        sbyte* inputPtr2 = inputP + 32;
                        ulong* outputPtr = (ulong*)outputP;
                        sbyte* finishPtr = inputP + input.Length;
                        while (inputPtr < finishPtr)
                        {
                            // Note: x2 unroll seems to be the sweet spot in benchmarks
                            var loaded1 = Avx.LoadVector256(inputPtr);
                            var loaded2 = Avx.LoadVector256(inputPtr2);
                            var shuffled1 = Avx2.Shuffle(loaded1, shuffleMask); // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                            var shuffled2 = Avx2.Shuffle(loaded2, shuffleMask);
                            var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 33686018, 33686018, 50529027, 50529027, 67372036, 67372036>
                            var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                            var permuted1_64 = permuted1.AsUInt64();
                            var permuted2_64 = permuted2.AsUInt64();

                            outputPtr[0] = permuted1_64[0];
                            outputPtr[1] = permuted2_64[0];

                            outputPtr[0 + ch2Offset64b] = permuted1_64[1];
                            outputPtr[1 + ch2Offset64b] = permuted2_64[1];

                            outputPtr[0 + ch3Offset64b] = permuted1_64[2];
                            outputPtr[1 + ch3Offset64b] = permuted2_64[2];

                            outputPtr[0 + ch4Offset64b] = permuted1_64[3];
                            outputPtr[1 + ch4Offset64b] = permuted2_64[3];

                            inputPtr += 64;
                            inputPtr2 += 64;
                            outputPtr += 2;
                        }
                    }
                }
            }
            else
            {
                if (input.Length % 4 != 0)
                    throw new ArgumentException($"Input length must be multiple of 4");

                int ch2Offset8b = channelBlockSizeBytes;
                int ch3Offset8b = (channelBlockSizeBytes * 2);
                int ch4Offset8b = (channelBlockSizeBytes * 3);
                unsafe
                {
                    fixed (sbyte* inputP = input)
                    fixed (sbyte* outputP = output)
                    {
                        sbyte* inputPtr = inputP;
                        sbyte* outputPtr = outputP;
                        sbyte* finishPtr = inputP + input.Length;
                        while (inputPtr < finishPtr)
                        {
                            outputPtr[0] = inputPtr[0];
                            outputPtr[0 + ch2Offset8b] = inputPtr[1];
                            outputPtr[0 + ch3Offset8b] = inputPtr[2];
                            outputPtr[0 + ch4Offset8b] = inputPtr[3];
                            inputPtr += 4;
                            outputPtr++;
                        }
                    }
                }
            }
        }

        public static void FourChannelsRunLength1VariantE(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int channelBlockSizeBytes = output.Length / 4;

            if (Avx2.IsSupported)       // Const after JIT/AOT
            {
                if (input.Length % 128 != 0)
                    throw new ArgumentException($"Input length must be multiple of 128");

                int ch2Offset64b = channelBlockSizeBytes / 8;
                int ch3Offset64b = (channelBlockSizeBytes * 2) / 8;
                int ch4Offset64b = (channelBlockSizeBytes * 3) / 8;
                Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
                Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
                unsafe
                {
                    fixed (sbyte* inputP = input)
                    fixed (sbyte* outputP = output)
                    {
                        sbyte* inputPtr = inputP;
                        sbyte* inputPtr2 = inputP + 32;
                        sbyte* inputPtr3 = inputP + 64;
                        sbyte* inputPtr4 = inputP + 96;
                        ulong* outputPtr = (ulong*)outputP;
                        sbyte* finishPtr = inputP + input.Length;
                        while (inputPtr < finishPtr)
                        {
                            // Note: x2 unroll seems to be the sweet spot in benchmarks
                            var loaded1 = Avx.LoadVector256(inputPtr);
                            var loaded2 = Avx.LoadVector256(inputPtr2);
                            var loaded3 = Avx.LoadVector256(inputPtr3);
                            var loaded4 = Avx.LoadVector256(inputPtr4);
                            var shuffled1 = Avx2.Shuffle(loaded1, shuffleMask); // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                            var shuffled2 = Avx2.Shuffle(loaded2, shuffleMask);
                            var shuffled3 = Avx2.Shuffle(loaded3, shuffleMask);
                            var shuffled4 = Avx2.Shuffle(loaded4, shuffleMask);
                            var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 33686018, 33686018, 50529027, 50529027, 67372036, 67372036>
                            var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                            var permuted3 = Avx2.PermuteVar8x32(shuffled3.AsInt32(), permuteMask);
                            var permuted4 = Avx2.PermuteVar8x32(shuffled4.AsInt32(), permuteMask);
                            var permuted1_64 = permuted1.AsUInt64();
                            var permuted2_64 = permuted2.AsUInt64();
                            var permuted3_64 = permuted3.AsUInt64();
                            var permuted4_64 = permuted4.AsUInt64();

                            outputPtr[0] = permuted1_64[0];
                            outputPtr[1] = permuted2_64[0];
                            outputPtr[2] = permuted3_64[0];
                            outputPtr[3] = permuted4_64[0];

                            outputPtr[0 + ch2Offset64b] = permuted1_64[1];
                            outputPtr[1 + ch2Offset64b] = permuted2_64[1];
                            outputPtr[2 + ch2Offset64b] = permuted3_64[1];
                            outputPtr[3 + ch2Offset64b] = permuted4_64[1];

                            outputPtr[0 + ch3Offset64b] = permuted1_64[2];
                            outputPtr[1 + ch3Offset64b] = permuted2_64[2];
                            outputPtr[2 + ch3Offset64b] = permuted3_64[2];
                            outputPtr[3 + ch3Offset64b] = permuted4_64[2];

                            outputPtr[0 + ch4Offset64b] = permuted1_64[3];
                            outputPtr[1 + ch4Offset64b] = permuted2_64[3];
                            outputPtr[2 + ch4Offset64b] = permuted3_64[3];
                            outputPtr[3 + ch4Offset64b] = permuted4_64[3];

                            inputPtr += 128;
                            inputPtr2 += 128;
                            inputPtr3 += 128;
                            inputPtr4 += 128;
                            outputPtr += 4;
                        }
                    }
                }
            }
            else
            {
                if (input.Length % 4 != 0)
                    throw new ArgumentException($"Input length must be multiple of 4");

                int ch2Offset8b = channelBlockSizeBytes;
                int ch3Offset8b = (channelBlockSizeBytes * 2);
                int ch4Offset8b = (channelBlockSizeBytes * 3);
                unsafe
                {
                    fixed (sbyte* inputP = input)
                    fixed (sbyte* outputP = output)
                    {
                        sbyte* inputPtr = inputP;
                        sbyte* outputPtr = outputP;
                        sbyte* finishPtr = inputP + input.Length;
                        while (inputPtr < finishPtr)
                        {
                            outputPtr[0] = inputPtr[0];
                            outputPtr[0 + ch2Offset8b] = inputPtr[1];
                            outputPtr[0 + ch3Offset8b] = inputPtr[2];
                            outputPtr[0 + ch4Offset8b] = inputPtr[3];
                            inputPtr += 4;
                            outputPtr++;
                        }
                    }
                }
            }
        }

        public static void FourChannelsNoSimd(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int channelBlockSize = output.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);

            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    sbyte* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        outputPtr[0] = inputPtr[0];
                        outputPtr[ch2Offset] = inputPtr[1];
                        outputPtr[ch3Offset] = inputPtr[2];
                        outputPtr[ch4Offset] = inputPtr[3];
                        inputPtr += 4;
                        outputPtr += 1;
                    }
                }
            }
        }

        public static void FourChannelsRunLength4(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException($"Input length must be multiple of 32");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int loopIterations = input.Length / 32;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
            Vector256<ulong> storeCh1Mask = Vector256.Create(ulong.MaxValue, 0, 0, 0);
            Vector256<ulong> storeCh2Mask = Vector256.Create(0, ulong.MaxValue, 0, 0);
            Vector256<ulong> storeCh3Mask = Vector256.Create(0, 0, ulong.MaxValue, 0);
            Vector256<ulong> storeCh4Mask = Vector256.Create(0, 0, 0, ulong.MaxValue);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 4;
            int ch2Offset = channelBlockSize - 1;
            int ch3Offset = (channelBlockSize * 2) - 2;
            int ch4Offset = (channelBlockSize * 3) - 3;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    for (int i = 0; i < loopIterations; i++)
                    {
                        Vector256<ulong> shuffledVector = Avx2.PermuteVar8x32(Avx.LoadVector256(inputPtr).AsInt32(), permuteMask).AsUInt64();
                        Avx2.MaskStore(outputPtr, storeCh1Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch2Offset, storeCh2Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch3Offset, storeCh3Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch4Offset, storeCh4Mask, shuffledVector);
                        inputPtr += 32;
                        outputPtr++;
                    }
                }
            }
        }

        public static void FourChannelsRunLength8(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException($"Input length must be multiple of 32");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int loopIterations = input.Length / 32;
            Vector256<ulong> storeCh1Mask = Vector256.Create(ulong.MaxValue, 0, 0, 0);
            Vector256<ulong> storeCh2Mask = Vector256.Create(0, ulong.MaxValue, 0, 0);
            Vector256<ulong> storeCh3Mask = Vector256.Create(0, 0, ulong.MaxValue, 0);
            Vector256<ulong> storeCh4Mask = Vector256.Create(0, 0, 0, ulong.MaxValue);
            Span<ulong> outputU64 = MemoryMarshal.Cast<sbyte, ulong>(output);
            int channelBlockSize = outputU64.Length / 4;
            int ch2Offset = channelBlockSize - 1;
            int ch3Offset = (channelBlockSize * 2) - 2;
            int ch4Offset = (channelBlockSize * 3) - 3;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (ulong* outputP = outputU64)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = outputP;
                    for (int i = 0; i < loopIterations; i++)
                    {
                        Vector256<ulong> shuffledVector = Avx.LoadVector256(inputPtr).AsUInt64();
                        Avx2.MaskStore(outputPtr, storeCh1Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch2Offset, storeCh2Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch3Offset, storeCh3Mask, shuffledVector);
                        Avx2.MaskStore(outputPtr + ch4Offset, storeCh4Mask, shuffledVector);
                        inputPtr += 32;
                        outputPtr++;
                    }
                }
            }
        }

        // This is the baseline 4-channel run length 32 algorithm for comparison purposes
        public static void FourChannelsRunLength32(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 128 != 0)
                throw new ArgumentException($"Input length must be multiple of 128");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int loopIterations = input.Length / 128;
            int channelBlockSize = output.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    sbyte* outputPtr = outputP;
                    for (int i = 0; i < loopIterations; i++)
                    {
                        Vector256<sbyte> inputVector = Avx.LoadVector256(inputPtr);
                        Avx.Store(outputPtr, inputVector);

                        inputVector = Avx.LoadVector256(inputPtr + 32);
                        Avx.Store(outputPtr + ch2Offset, inputVector);

                        inputVector = Avx.LoadVector256(inputPtr + 64);
                        Avx.Store(outputPtr + ch3Offset, inputVector);

                        inputVector = Avx.LoadVector256(inputPtr + 96);
                        Avx.Store(outputPtr + ch4Offset, inputVector);

                        inputPtr += 128;
                        outputPtr += 32;
                    }
                }
            }
        }

        public static void FourChannelsRunLength32VariantA(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length % 256 != 0)
                throw new ArgumentException($"Input length must be multiple of 256");
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int loopIterations = input.Length / 256;
            int channelBlockSize = output.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    sbyte* outputPtr = outputP;
                    for (int i = 0; i < loopIterations; i++)
                    {
                        Vector256<sbyte> inputVector1 = Avx.LoadVector256(inputPtr);
                        Vector256<sbyte> inputVector2 = Avx.LoadVector256(inputPtr + 32);
                        Vector256<sbyte> inputVector3 = Avx.LoadVector256(inputPtr + 64);
                        Vector256<sbyte> inputVector4 = Avx.LoadVector256(inputPtr + 96);
                        Vector256<sbyte> inputVector5 = Avx.LoadVector256(inputPtr + 128);
                        Vector256<sbyte> inputVector6 = Avx.LoadVector256(inputPtr + 160);
                        Vector256<sbyte> inputVector7 = Avx.LoadVector256(inputPtr + 192);
                        Vector256<sbyte> inputVector8 = Avx.LoadVector256(inputPtr + 224);
                        Avx.Store(outputPtr, inputVector1);
                        Avx.Store(outputPtr + ch2Offset, inputVector2);
                        Avx.Store(outputPtr + ch3Offset, inputVector3);
                        Avx.Store(outputPtr + ch4Offset, inputVector4);
                        Avx.Store(outputPtr + 32, inputVector5);
                        Avx.Store(outputPtr + ch2Offset + 32, inputVector6);
                        Avx.Store(outputPtr + ch3Offset + 32, inputVector7);
                        Avx.Store(outputPtr + ch4Offset + 32, inputVector8);

                        inputPtr += 256;
                        outputPtr += 64;
                    }
                }
            }
        }

        public static void FourChannelsRunLength32NoSimd(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int channelBlockSize = output.Length / 4;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);

            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    sbyte* outputPtr = outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        outputPtr[0] = inputPtr[0];
                        outputPtr[1] = inputPtr[1];
                        outputPtr[2] = inputPtr[2];
                        outputPtr[3] = inputPtr[3];
                        outputPtr[4] = inputPtr[4];
                        outputPtr[5] = inputPtr[5];
                        outputPtr[6] = inputPtr[6];
                        outputPtr[7] = inputPtr[7];
                        outputPtr[8] = inputPtr[8];
                        outputPtr[9] = inputPtr[9];
                        outputPtr[10] = inputPtr[10];
                        outputPtr[11] = inputPtr[11];
                        outputPtr[12] = inputPtr[12];
                        outputPtr[13] = inputPtr[13];
                        outputPtr[14] = inputPtr[14];
                        outputPtr[15] = inputPtr[15];
                        outputPtr[16] = inputPtr[16];
                        outputPtr[17] = inputPtr[17];
                        outputPtr[18] = inputPtr[18];
                        outputPtr[19] = inputPtr[19];
                        outputPtr[20] = inputPtr[20];
                        outputPtr[21] = inputPtr[21];
                        outputPtr[22] = inputPtr[22];
                        outputPtr[23] = inputPtr[23];
                        outputPtr[24] = inputPtr[24];
                        outputPtr[25] = inputPtr[25];
                        outputPtr[26] = inputPtr[26];
                        outputPtr[27] = inputPtr[27];
                        outputPtr[28] = inputPtr[28];
                        outputPtr[29] = inputPtr[29];
                        outputPtr[30] = inputPtr[30];
                        outputPtr[31] = inputPtr[31];

                        outputPtr[ch2Offset + 0] = inputPtr[32 + 0];
                        outputPtr[ch2Offset + 1] = inputPtr[32 + 1];
                        outputPtr[ch2Offset + 2] = inputPtr[32 + 2];
                        outputPtr[ch2Offset + 3] = inputPtr[32 + 3];
                        outputPtr[ch2Offset + 4] = inputPtr[32 + 4];
                        outputPtr[ch2Offset + 5] = inputPtr[32 + 5];
                        outputPtr[ch2Offset + 6] = inputPtr[32 + 6];
                        outputPtr[ch2Offset + 7] = inputPtr[32 + 7];
                        outputPtr[ch2Offset + 8] = inputPtr[32 + 8];
                        outputPtr[ch2Offset + 9] = inputPtr[32 + 9];
                        outputPtr[ch2Offset + 10] = inputPtr[32 + 10];
                        outputPtr[ch2Offset + 11] = inputPtr[32 + 11];
                        outputPtr[ch2Offset + 12] = inputPtr[32 + 12];
                        outputPtr[ch2Offset + 13] = inputPtr[32 + 13];
                        outputPtr[ch2Offset + 14] = inputPtr[32 + 14];
                        outputPtr[ch2Offset + 15] = inputPtr[32 + 15];
                        outputPtr[ch2Offset + 16] = inputPtr[32 + 16];
                        outputPtr[ch2Offset + 17] = inputPtr[32 + 17];
                        outputPtr[ch2Offset + 18] = inputPtr[32 + 18];
                        outputPtr[ch2Offset + 19] = inputPtr[32 + 19];
                        outputPtr[ch2Offset + 20] = inputPtr[32 + 20];
                        outputPtr[ch2Offset + 21] = inputPtr[32 + 21];
                        outputPtr[ch2Offset + 22] = inputPtr[32 + 22];
                        outputPtr[ch2Offset + 23] = inputPtr[32 + 23];
                        outputPtr[ch2Offset + 24] = inputPtr[32 + 24];
                        outputPtr[ch2Offset + 25] = inputPtr[32 + 25];
                        outputPtr[ch2Offset + 26] = inputPtr[32 + 26];
                        outputPtr[ch2Offset + 27] = inputPtr[32 + 27];
                        outputPtr[ch2Offset + 28] = inputPtr[32 + 28];
                        outputPtr[ch2Offset + 29] = inputPtr[32 + 29];
                        outputPtr[ch2Offset + 30] = inputPtr[32 + 30];
                        outputPtr[ch2Offset + 31] = inputPtr[32 + 31];

                        outputPtr[ch3Offset + 0] = inputPtr[64 + 0];
                        outputPtr[ch3Offset + 1] = inputPtr[64 + 1];
                        outputPtr[ch3Offset + 2] = inputPtr[64 + 2];
                        outputPtr[ch3Offset + 3] = inputPtr[64 + 3];
                        outputPtr[ch3Offset + 4] = inputPtr[64 + 4];
                        outputPtr[ch3Offset + 5] = inputPtr[64 + 5];
                        outputPtr[ch3Offset + 6] = inputPtr[64 + 6];
                        outputPtr[ch3Offset + 7] = inputPtr[64 + 7];
                        outputPtr[ch3Offset + 8] = inputPtr[64 + 8];
                        outputPtr[ch3Offset + 9] = inputPtr[64 + 9];
                        outputPtr[ch3Offset + 10] = inputPtr[64 + 10];
                        outputPtr[ch3Offset + 11] = inputPtr[64 + 11];
                        outputPtr[ch3Offset + 12] = inputPtr[64 + 12];
                        outputPtr[ch3Offset + 13] = inputPtr[64 + 13];
                        outputPtr[ch3Offset + 14] = inputPtr[64 + 14];
                        outputPtr[ch3Offset + 15] = inputPtr[64 + 15];
                        outputPtr[ch3Offset + 16] = inputPtr[64 + 16];
                        outputPtr[ch3Offset + 17] = inputPtr[64 + 17];
                        outputPtr[ch3Offset + 18] = inputPtr[64 + 18];
                        outputPtr[ch3Offset + 19] = inputPtr[64 + 19];
                        outputPtr[ch3Offset + 20] = inputPtr[64 + 20];
                        outputPtr[ch3Offset + 21] = inputPtr[64 + 21];
                        outputPtr[ch3Offset + 22] = inputPtr[64 + 22];
                        outputPtr[ch3Offset + 23] = inputPtr[64 + 23];
                        outputPtr[ch3Offset + 24] = inputPtr[64 + 24];
                        outputPtr[ch3Offset + 25] = inputPtr[64 + 25];
                        outputPtr[ch3Offset + 26] = inputPtr[64 + 26];
                        outputPtr[ch3Offset + 27] = inputPtr[64 + 27];
                        outputPtr[ch3Offset + 28] = inputPtr[64 + 28];
                        outputPtr[ch3Offset + 29] = inputPtr[64 + 29];
                        outputPtr[ch3Offset + 30] = inputPtr[64 + 30];
                        outputPtr[ch3Offset + 31] = inputPtr[64 + 31];

                        outputPtr[ch4Offset + 0] = inputPtr[32 + 0];
                        outputPtr[ch4Offset + 1] = inputPtr[96 + 1];
                        outputPtr[ch4Offset + 2] = inputPtr[96 + 2];
                        outputPtr[ch4Offset + 3] = inputPtr[96 + 3];
                        outputPtr[ch4Offset + 4] = inputPtr[96 + 4];
                        outputPtr[ch4Offset + 5] = inputPtr[96 + 5];
                        outputPtr[ch4Offset + 6] = inputPtr[96 + 6];
                        outputPtr[ch4Offset + 7] = inputPtr[96 + 7];
                        outputPtr[ch4Offset + 8] = inputPtr[96 + 8];
                        outputPtr[ch4Offset + 9] = inputPtr[96 + 9];
                        outputPtr[ch4Offset + 10] = inputPtr[96 + 10];
                        outputPtr[ch4Offset + 11] = inputPtr[96 + 11];
                        outputPtr[ch4Offset + 12] = inputPtr[96 + 12];
                        outputPtr[ch4Offset + 13] = inputPtr[96 + 13];
                        outputPtr[ch4Offset + 14] = inputPtr[96 + 14];
                        outputPtr[ch4Offset + 15] = inputPtr[96 + 15];
                        outputPtr[ch4Offset + 16] = inputPtr[96 + 16];
                        outputPtr[ch4Offset + 17] = inputPtr[96 + 17];
                        outputPtr[ch4Offset + 18] = inputPtr[96 + 18];
                        outputPtr[ch4Offset + 19] = inputPtr[96 + 19];
                        outputPtr[ch4Offset + 20] = inputPtr[96 + 20];
                        outputPtr[ch4Offset + 21] = inputPtr[96 + 21];
                        outputPtr[ch4Offset + 22] = inputPtr[96 + 22];
                        outputPtr[ch4Offset + 23] = inputPtr[96 + 23];
                        outputPtr[ch4Offset + 24] = inputPtr[96 + 24];
                        outputPtr[ch4Offset + 25] = inputPtr[96 + 25];
                        outputPtr[ch4Offset + 26] = inputPtr[96 + 26];
                        outputPtr[ch4Offset + 27] = inputPtr[96 + 27];
                        outputPtr[ch4Offset + 28] = inputPtr[96 + 28];
                        outputPtr[ch4Offset + 29] = inputPtr[96 + 29];
                        outputPtr[ch4Offset + 30] = inputPtr[96 + 30];
                        outputPtr[ch4Offset + 31] = inputPtr[96 + 31];
                        inputPtr += 128;
                        outputPtr += 32;
                    }
                }
            }
        }

        public static void FourChannelsRunLength32NoSimd2(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int channelBlockSize = output.Length / 4 / 8;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);

            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    ulong* inputPtr = (ulong*)inputP;
                    ulong* outputPtr = (ulong*)outputP;
                    ulong* finishPtr = inputPtr + (input.Length / 8);
                    while (inputPtr < finishPtr)
                    {
                        outputPtr[0] = inputPtr[0];
                        outputPtr[1] = inputPtr[1];
                        outputPtr[2] = inputPtr[2];
                        outputPtr[3] = inputPtr[3];

                        outputPtr[ch2Offset + 0] = inputPtr[4];
                        outputPtr[ch2Offset + 1] = inputPtr[5];
                        outputPtr[ch2Offset + 2] = inputPtr[6];
                        outputPtr[ch2Offset + 3] = inputPtr[7];

                        outputPtr[ch3Offset + 0] = inputPtr[8];
                        outputPtr[ch3Offset + 1] = inputPtr[9];
                        outputPtr[ch3Offset + 2] = inputPtr[10];
                        outputPtr[ch3Offset + 3] = inputPtr[11];

                        outputPtr[ch4Offset + 0] = inputPtr[12];
                        outputPtr[ch4Offset + 1] = inputPtr[13];
                        outputPtr[ch4Offset + 2] = inputPtr[14];
                        outputPtr[ch4Offset + 3] = inputPtr[15];
                        inputPtr += 16;
                        outputPtr += 4;
                    }
                }
            }
        }

        public static void FourChannelsRunLength32NoSimd3(ReadOnlySpan<sbyte> input, Span<sbyte> output)
        {
            if (input.Length != output.Length)
                throw new ArgumentException("Array lengths must match");

            int channelBlockSize = output.Length / 4 / 8;
            int ch2Offset = channelBlockSize;
            int ch3Offset = (channelBlockSize * 2);
            int ch4Offset = (channelBlockSize * 3);

            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    ulong* inputPtr = (ulong*)inputP;
                    ulong* outputPtr = (ulong*)outputP;
                    ulong* finishPtr = inputPtr + (input.Length / 8);
                    while (inputPtr < finishPtr)
                    {
                        outputPtr[0] = inputPtr[0];
                        outputPtr[1] = inputPtr[1];
                        outputPtr[ch2Offset + 0] = inputPtr[2];
                        outputPtr[ch2Offset + 1] = inputPtr[3];
                        outputPtr[ch3Offset + 0] = inputPtr[4];
                        outputPtr[ch3Offset + 1] = inputPtr[5];
                        outputPtr[ch4Offset + 0] = inputPtr[6];
                        outputPtr[ch4Offset + 1] = inputPtr[7];

                        inputPtr += 8;
                        outputPtr += 2;
                    }
                }
            }
        }
    }
}
