using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace TS.NET;

public static class ShuffleI8
{
    public static void FourChannels(ReadOnlySpan<sbyte> input, Span<sbyte> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSizeBytes = output.Length / 4;

        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            var processingLength = Vector256<sbyte>.Count * 4;  // 128
            if (input.Length % processingLength != 0) throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            int ch3Offset = channelBlockSizeBytes * 2;
            int ch4Offset = channelBlockSizeBytes * 3;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
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
                        var loaded1 = Vector256.LoadAlignedNonTemporal(inputPtr);
                        var loaded2 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<sbyte>.Count);
                        var loaded3 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<sbyte>.Count * 2);
                        var loaded4 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<sbyte>.Count * 3);
                        var shuffled1 = Avx2.Shuffle(loaded1, shuffleMask);
                        var shuffled2 = Avx2.Shuffle(loaded2, shuffleMask);
                        var shuffled3 = Avx2.Shuffle(loaded3, shuffleMask);
                        var shuffled4 = Avx2.Shuffle(loaded4, shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask).AsUInt64();
                        var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask).AsUInt64();
                        var permuted3 = Avx2.PermuteVar8x32(shuffled3.AsInt32(), permuteMask).AsUInt64();
                        var permuted4 = Avx2.PermuteVar8x32(shuffled4.AsInt32(), permuteMask).AsUInt64();
                        var unpackLow = Avx2.UnpackLow(permuted1, permuted2);
                        var unpackLow2 = Avx2.UnpackLow(permuted3, permuted4);
                        var channel1 = Avx2.Permute2x128(unpackLow, unpackLow2, 0x20).AsSByte();
                        var channel3 = Avx2.Permute2x128(unpackLow, unpackLow2, 0x31).AsSByte();
                        var unpackHigh = Avx2.UnpackHigh(permuted1, permuted2);
                        var unpackHigh2 = Avx2.UnpackHigh(permuted3, permuted4);
                        var channel2 = Avx2.Permute2x128(unpackHigh, unpackHigh2, 0x20).AsSByte();
                        var channel4 = Avx2.Permute2x128(unpackHigh, unpackHigh2, 0x31).AsSByte();
                        Vector256.StoreAlignedNonTemporal(channel1, outputPtr);
                        Vector256.StoreAlignedNonTemporal(channel2, outputPtr + ch2Offset);
                        Vector256.StoreAlignedNonTemporal(channel3, outputPtr + ch3Offset);
                        Vector256.StoreAlignedNonTemporal(channel4, outputPtr + ch4Offset);
                        inputPtr += processingLength;
                        outputPtr += Vector256<sbyte>.Count;
                    }
                }
            }
        }
        else if (Ssse3.IsSupported)
        {
            var processingLength = Vector128<sbyte>.Count * 4;  // 64
            if (input.Length % processingLength != 0) throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            int ch3Offset = channelBlockSizeBytes * 2;
            int ch4Offset = channelBlockSizeBytes * 3;
            Vector128<sbyte> shuffleMask = Vector128.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
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
                        var loaded1 = Vector128.LoadAlignedNonTemporal(inputPtr);
                        var loaded2 = Vector128.LoadAlignedNonTemporal(inputPtr + Vector128<sbyte>.Count);
                        var loaded3 = Vector128.LoadAlignedNonTemporal(inputPtr + Vector128<sbyte>.Count * 2);
                        var loaded4 = Vector128.LoadAlignedNonTemporal(inputPtr + Vector128<sbyte>.Count * 3);
                        var shuffled1 = Ssse3.Shuffle(loaded1, shuffleMask);
                        var shuffled2 = Ssse3.Shuffle(loaded2, shuffleMask);
                        var shuffled3 = Ssse3.Shuffle(loaded3, shuffleMask);
                        var shuffled4 = Ssse3.Shuffle(loaded4, shuffleMask);
                        var unpackLow = Sse2.UnpackLow(shuffled1.AsUInt32(), shuffled2.AsUInt32()).AsUInt64();
                        var unpackLow2 = Sse2.UnpackLow(shuffled3.AsUInt32(), shuffled4.AsUInt32()).AsUInt64();
                        var channel1 = Sse2.UnpackLow(unpackLow.AsUInt64(), unpackLow2.AsUInt64()).AsSByte();
                        var channel2 = Sse2.UnpackHigh(unpackLow.AsUInt64(), unpackLow2.AsUInt64()).AsSByte();
                        var unpackHigh = Sse2.UnpackHigh(shuffled1.AsUInt32(), shuffled2.AsUInt32()).AsUInt64();
                        var unpackHigh2 = Sse2.UnpackHigh(shuffled3.AsUInt32(), shuffled4.AsUInt32()).AsUInt64();
                        var channel3 = Sse2.UnpackLow(unpackHigh.AsUInt64(), unpackHigh2.AsUInt64()).AsSByte();
                        var channel4 = Sse2.UnpackHigh(unpackHigh.AsUInt64(), unpackHigh2.AsUInt64()).AsSByte();
                        Vector128.StoreAlignedNonTemporal(channel1, outputPtr);
                        Vector128.StoreAlignedNonTemporal(channel2, outputPtr + ch2Offset);
                        Vector128.StoreAlignedNonTemporal(channel3, outputPtr + ch3Offset);
                        Vector128.StoreAlignedNonTemporal(channel4, outputPtr + ch4Offset);
                        inputPtr += processingLength;
                        outputPtr += Vector128<sbyte>.Count;
                    }
                }
            }
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            var processingLength = Vector128<sbyte>.Count * 4;  // 64
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            int ch3Offset = channelBlockSizeBytes * 2;
            int ch4Offset = channelBlockSizeBytes * 3;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    // Naive way
                    // var loaded1 = AdvSimd.LoadVector128(inputPtr);  // 0,1,2,3,0,1,2,3,0,1,2,3,0,1,2,3
                    // var shuffled = AdvSimd.Arm64.VectorTableLookup(loaded1, Vector128.Create(0,4,8,12,1,5,9,13,2,6,10,14,3,7,11,15).AsSByte()).AsUInt32();  // 0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3

                    byte* inputPtr = (byte*)inputP;
                    byte* outputPtr = (byte*)outputP;
                    byte* finishPtr = (byte*)inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        // Loop unrolling doesn't improve performance on Apple M4.
                        var loaded = AdvSimd.Arm64.Load4xVector128AndUnzip(inputPtr);
                        AdvSimd.Store(outputPtr, loaded.Value1);
                        AdvSimd.Store(outputPtr + ch2Offset, loaded.Value2);
                        AdvSimd.Store(outputPtr + ch3Offset, loaded.Value3);
                        AdvSimd.Store(outputPtr + ch4Offset, loaded.Value4);
                        inputPtr += processingLength;
                        outputPtr += Vector128<sbyte>.Count;
                    }
                }
            }
        }
        else
        {
            var processingLength = 4;
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            int ch3Offset = channelBlockSizeBytes * 2;
            int ch4Offset = channelBlockSizeBytes * 3;
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
                        outputPtr[0 + ch2Offset] = inputPtr[1];
                        outputPtr[0 + ch3Offset] = inputPtr[2];
                        outputPtr[0 + ch4Offset] = inputPtr[3];
                        inputPtr += processingLength;
                        outputPtr += 1;
                    }
                }
            }
        }
    }

    public static void TwoChannels(ReadOnlySpan<sbyte> input, Span<sbyte> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSizeBytes = output.Length / 2;

        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            var processingLength = Vector256<sbyte>.Count * 2;  // 64
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15, 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);
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
                        var loaded1 = Vector256.LoadAlignedNonTemporal(inputPtr);
                        var loaded2 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<sbyte>.Count);
                        var shuffled1 = Avx2.Shuffle(loaded1, shuffleMask);
                        var shuffled2 = Avx2.Shuffle(loaded2, shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);
                        var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                        var channel1 = Avx2.Permute2x128(permuted1, permuted2, 0x20).AsSByte();
                        var channel2 = Avx2.Permute2x128(permuted1, permuted2, 0x31).AsSByte();
                        Vector256.StoreAlignedNonTemporal(channel1, outputPtr);
                        Vector256.StoreAlignedNonTemporal(channel2, outputPtr + ch2Offset);
                        inputPtr += processingLength;
                        outputPtr += Vector256<sbyte>.Count;
                    }
                }
            }
        }
        else if (Ssse3.IsSupported)
        {
            var processingLength = Vector128<sbyte>.Count * 2;  // 32
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            Vector128<sbyte> shuffleMask = Vector128.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
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
                        var loaded1 = Vector128.LoadAlignedNonTemporal(inputPtr);
                        var loaded2 = Vector128.LoadAlignedNonTemporal(inputPtr + Vector128<sbyte>.Count);
                        var shuffled1 = Ssse3.Shuffle(loaded1, shuffleMask);
                        var shuffled2 = Ssse3.Shuffle(loaded2, shuffleMask);
                        var channel1 = Sse2.UnpackLow(shuffled1.AsUInt64(), shuffled2.AsUInt64()).AsSByte();
                        var channel2 = Sse2.UnpackHigh(shuffled1.AsUInt64(), shuffled2.AsUInt64()).AsSByte();
                        Vector128.StoreAlignedNonTemporal(channel1, outputPtr);
                        Vector128.StoreAlignedNonTemporal(channel2, outputPtr + ch2Offset);
                        inputPtr += processingLength;
                        outputPtr += Vector128<sbyte>.Count;
                    }
                }
            }
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            var processingLength = Vector128<sbyte>.Count * 2;  // 32
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    byte* inputPtr = (byte*)inputP;
                    byte* outputPtr = (byte*)outputP;
                    byte* finishPtr = (byte*)inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        // Loop unrolling doesn't improve performance on Apple M4.
                        var loaded1 = AdvSimd.Arm64.Load2xVector128AndUnzip(inputPtr);
                        AdvSimd.Store(outputPtr, loaded1.Value1);
                        AdvSimd.Store(outputPtr + ch2Offset, loaded1.Value2);
                        inputPtr += processingLength;
                        outputPtr += Vector128<sbyte>.Count;
                    }
                }
            }
        }
        else
        {
            var processingLength = 2;
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSizeBytes;
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
                        outputPtr[0 + ch2Offset] = inputPtr[1];
                        inputPtr += processingLength;
                        outputPtr += 1;
                    }
                }
            }
        }
    }
}