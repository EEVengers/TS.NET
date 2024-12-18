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
                    sbyte* inputPtr2 = inputP + Vector256<sbyte>.Count;     // Separate pointer needed to get vmovups to bunch up at the top of the asm
                    ulong* outputPtr = (ulong*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        // Note: x2 unroll seems to be the sweet spot in benchmarks
                        var loaded1 = Avx.LoadVector256(inputPtr);
                        var loaded2 = Avx.LoadVector256(inputPtr2);
                        var shuffled1 = Avx2.Shuffle(loaded1, shuffleMask); // shuffled1 = <1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4>
                        var shuffled2 = Avx2.Shuffle(loaded2, shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4>
                        var permuted2 = Avx2.PermuteVar8x32(shuffled2.AsInt32(), permuteMask);
                        var permuted1_64 = permuted1.AsUInt64();
                        var permuted2_64 = permuted2.AsUInt64();
                        var unpackHigh = Avx2.UnpackHigh(permuted1_64, permuted2_64);   // unpackHigh = <2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4>
                        var unpackLow = Avx2.UnpackLow(permuted1_64, permuted2_64);     // unpackLow = <1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3>

                        Vector128.Store(unpackLow.GetLower(), outputPtr);
                        Vector128.Store(unpackHigh.GetLower(), outputPtr + ch2Offset64b);
                        Vector128.Store(unpackLow.GetUpper(), outputPtr + ch3Offset64b);
                        Vector128.Store(unpackHigh.GetUpper(), outputPtr + ch4Offset64b);

                        inputPtr += Vector256<sbyte>.Count * 2;
                        inputPtr2 += Vector256<sbyte>.Count * 2;
                        outputPtr += 2;
                    }
                }
            }
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            if (input.Length % 16 != 0)
                throw new ArgumentException($"Input length must be multiple of 16");

            var tableLookup01 = Vector64.Create(0,4,8,12,1,5,9,13).AsSByte();
            var tableLookup23 = Vector64.Create(2,6,10,14,3,7,11,15).AsSByte();
            int ch2Offset8b = channelBlockSizeBytes / 4;
            int ch3Offset8b = (channelBlockSizeBytes * 2) / 4;
            int ch4Offset8b = (channelBlockSizeBytes * 3) / 4;
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    uint* outputPtr = (uint*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var loaded1 = AdvSimd.LoadVector128(inputPtr);  // 0,1,2,3,0,1,2,3,0,1,2,3,0,1,2,3
                        var shuffled01 = AdvSimd.VectorTableLookup(loaded1, tableLookup01).AsUInt32(); // 0,0,0,0,1,1,1,1
                        var shuffled23 = AdvSimd.VectorTableLookup(loaded1, tableLookup23).AsUInt32(); // 2,2,2,2,3,3,3,3
                        outputPtr[0] = shuffled01[0];
                        outputPtr[0 + ch2Offset8b] = shuffled01[1];
                        outputPtr[0 + ch3Offset8b] = shuffled23[0];
                        outputPtr[0 + ch4Offset8b] = shuffled23[1];

                        inputPtr += Vector128<sbyte>.Count;
                        outputPtr += 1;
                    }
                }
            }   
        }
        else
        {
            if (input.Length % 4 != 0)
                throw new ArgumentException($"Input length must be multiple of 4");

            int ch2Offset8b = channelBlockSizeBytes;
            int ch3Offset8b = channelBlockSizeBytes * 2;
            int ch4Offset8b = channelBlockSizeBytes * 3;
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

    public static void TwoChannels(ReadOnlySpan<sbyte> input, Span<sbyte> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSizeBytes = output.Length / 2;

        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException($"Length of samples ({input.Length}) is not multiple of 32");

            int ch2Offset64b = channelBlockSizeBytes / 8;
            Vector256<sbyte> shuffleMask = Vector256.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15, 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
            Vector256<int> permuteMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);
            unsafe
            {
                fixed (sbyte* inputP = input)
                fixed (sbyte* outputP = output)
                {
                    sbyte* inputPtr = inputP;
                    ulong* outputPtr = (ulong*)outputP;
                    sbyte* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        var shuffled1 = Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask); // shuffled1 = <1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2>
                        var permuted1 = Avx2.PermuteVar8x32(shuffled1.AsInt32(), permuteMask);  // permuted1 = <16843009, 16843009, 16843009, 16843009, 33686018, 33686018, 33686018, 33686018>
                        var permuted1_64 = permuted1.AsUInt64();
                        outputPtr[0] = permuted1_64[0];
                        outputPtr[1] = permuted1_64[1];
                        outputPtr[0 + ch2Offset64b] = permuted1_64[2];
                        outputPtr[1 + ch2Offset64b] = permuted1_64[3];
                        inputPtr += 32;
                        outputPtr += 2;
                    }
                }
            }
        }
        else
        {
            if (input.Length % 2 != 0)
                throw new ArgumentException($"Input length must be multiple of 2");

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
    }
}