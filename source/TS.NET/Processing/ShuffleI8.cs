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
            var processingLength = Vector256<sbyte>.Count * 2;  // 64
            if (input.Length % processingLength != 0) throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset_64 = channelBlockSizeBytes / 8;
            int ch3Offset_64 = (channelBlockSizeBytes * 2) / 8;
            int ch4Offset_64 = (channelBlockSizeBytes * 3) / 8;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr_64 = (ulong*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        // Note: x2 unroll seems to be the sweet spot in benchmarks, allowing for 128 bit stores
                        var loaded1 = Avx.LoadVector256(inputPtr);
                        var loaded2 = Avx.LoadVector256(inputPtr + Vector256<sbyte>.Count);
                        var shuffled1 = Avx2.Shuffle(loaded1, shuffleMask); // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                        var shuffled2 = Avx2.Shuffle(loaded2, shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4>
                        var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                        var permuted1_64 = permuted1.AsUInt64();
                        var permuted2_64 = permuted2.AsUInt64();
                        var unpackHigh = Avx2.UnpackHigh(permuted1_64, permuted2_64);   // unpackHigh = <2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4>
                        var unpackLow = Avx2.UnpackLow(permuted1_64, permuted2_64);     // unpackLow = <1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3>

                        Vector128.Store(unpackLow.GetLower(), outputPtr_64);
                        Vector128.Store(unpackHigh.GetLower(), outputPtr_64 + ch2Offset_64);
                        Vector128.Store(unpackLow.GetUpper(), outputPtr_64 + ch3Offset_64);
                        Vector128.Store(unpackHigh.GetUpper(), outputPtr_64 + ch4Offset_64);

                        inputPtr += processingLength;
                        outputPtr_64 += 2;
                    }
                }
            }
        }
        else if (Ssse3.IsSupported)
        {
            var processingLength = Vector128<sbyte>.Count * 2;  // 32
            if (input.Length % processingLength != 0) throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset_32 = channelBlockSizeBytes / 4;
            int ch3Offset_32 = (channelBlockSizeBytes * 2) / 4;
            int ch4Offset_32 = (channelBlockSizeBytes * 3) / 4;
            Vector128<sbyte> shuffleMask = Vector128.Create(0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15).AsSByte();
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    uint* outputPtr_32 = (uint*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var loaded1 = Sse2.LoadVector128(inputPtr);
                        var loaded2 = Sse2.LoadVector128(inputPtr + Vector128<sbyte>.Count);
                        var shuffled1 = Ssse3.Shuffle(loaded1, shuffleMask);
                        var shuffled2 = Ssse3.Shuffle(loaded2, shuffleMask);

                        var shuffled1_32 = shuffled1.AsUInt32();
                        var shuffled2_32 = shuffled2.AsUInt32();
                        outputPtr_32[0] = shuffled1_32[0];
                        outputPtr_32[1] = shuffled2_32[0];
                        outputPtr_32[0 + ch2Offset_32] = shuffled1_32[1];
                        outputPtr_32[1 + ch2Offset_32] = shuffled2_32[1];
                        outputPtr_32[0 + ch3Offset_32] = shuffled1_32[2];
                        outputPtr_32[1 + ch3Offset_32] = shuffled2_32[2];
                        outputPtr_32[0 + ch4Offset_32] = shuffled1_32[3];
                        outputPtr_32[1 + ch4Offset_32] = shuffled2_32[3];

                        inputPtr += processingLength;
                        outputPtr_32 += 2;
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
                        outputPtr += 16;
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
            var processingLength = Vector256<sbyte>.Count;  // 32
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset_64 = channelBlockSizeBytes / 8;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15, 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr_64 = (ulong*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask); // shuffled1 = <1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2>
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 16843009, 16843009, 33686018, 33686018, 33686018, 33686018>
                        var permuted1_64 = permuted1.AsUInt64();
                        outputPtr_64[0] = permuted1_64[0];
                        outputPtr_64[1] = permuted1_64[1];
                        outputPtr_64[0 + ch2Offset_64] = permuted1_64[2];
                        outputPtr_64[1 + ch2Offset_64] = permuted1_64[3];
                        inputPtr += processingLength;
                        outputPtr_64 += 2;
                    }
                }
            }
        }
        else if (Ssse3.IsSupported)
        {
            var processingLength = Vector128<sbyte>.Count;  // 16
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset_64 = channelBlockSizeBytes / 8;
            Vector128<sbyte> shuffleMask = Vector128.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr_64 = (ulong*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Ssse3.Shuffle(Sse2.LoadVector128(inputPtr), shuffleMask);
                        var shuffled1_64 = shuffled1.AsUInt64();
                        outputPtr_64[0] = shuffled1_64[0];
                        outputPtr_64[0 + ch2Offset_64] = shuffled1_64[1];
                        inputPtr += processingLength;
                        outputPtr_64 += 1;
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
                        outputPtr += 16;
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