using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public static class ShuffleI16
{
    public static void FourChannels(ReadOnlySpan<short> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSize = output.Length / 4;

        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            int processingLength = Vector256<short>.Count * 4;
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSize;
            int ch3Offset = channelBlockSize * 2;
            int ch4Offset = channelBlockSize * 3;
            Vector256<sbyte> shuffleMask = Vector256.Create(
                0, 1, 8, 9, 2, 3, 10, 11, 4, 5, 12, 13, 6, 7, 14, 15,       // 128-bit lane
                0, 1, 8, 9, 2, 3, 10, 11, 4, 5, 12, 13, 6, 7, 14, 15);      // 128-bit lane
            Vector256<int> permuteMask = Vector256.Create(0, 4, 2, 6, 1, 5, 3, 7);
            unsafe
            {
                fixed (short* inputP = input)
                fixed (short* outputP = output)
                {
                    short* inputPtr = inputP;
                    short* outputPtr = outputP;
                    short* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        // Loads are non-temporal because the data is never used again. Store as normal.
                        var loaded1 = Vector256.LoadAlignedNonTemporal(inputPtr);
                        var loaded2 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<short>.Count);
                        var loaded3 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<short>.Count * 2);
                        var loaded4 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<short>.Count * 3);
                        var shuffle1 = Avx2.Shuffle(loaded1.AsSByte(), shuffleMask).AsInt32();
                        var shuffle2 = Avx2.Shuffle(loaded2.AsSByte(), shuffleMask).AsInt32();
                        var shuffle3 = Avx2.Shuffle(loaded3.AsSByte(), shuffleMask).AsInt32();
                        var shuffle4 = Avx2.Shuffle(loaded4.AsSByte(), shuffleMask).AsInt32();
                        var unpackLow1 = Avx2.UnpackLow(shuffle1, shuffle2);
                        var unpackHigh1 = Avx2.UnpackHigh(shuffle1, shuffle2);
                        var unpackLow2 = Avx2.UnpackLow(shuffle3, shuffle4);
                        var unpackHigh2 = Avx2.UnpackHigh(shuffle3, shuffle4);
                        var unpackLow3 = Avx2.UnpackLow(unpackLow1, unpackLow2);
                        var unpackHigh3 = Avx2.UnpackHigh(unpackLow1, unpackLow2);
                        var unpackLow4 = Avx2.UnpackLow(unpackHigh1, unpackHigh2);
                        var unpackHigh4 = Avx2.UnpackHigh(unpackHigh1, unpackHigh2);
                        var channel1 = Avx2.PermuteVar8x32(unpackLow3, permuteMask).AsInt16();
                        var channel2 = Avx2.PermuteVar8x32(unpackHigh3, permuteMask).AsInt16();
                        var channel3 = Avx2.PermuteVar8x32(unpackLow4, permuteMask).AsInt16();
                        var channel4 = Avx2.PermuteVar8x32(unpackHigh4, permuteMask).AsInt16();
                        Vector256.StoreAligned(channel1, outputPtr);
                        Vector256.StoreAligned(channel2, outputPtr + ch2Offset);
                        Vector256.StoreAligned(channel3, outputPtr + ch3Offset);
                        Vector256.StoreAligned(channel4, outputPtr + ch4Offset);
                        inputPtr += processingLength;
                        outputPtr += Vector256<short>.Count;
                    }
                }
            }
        }
        else
        {
            var processingLength = 4;
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSize;
            int ch3Offset = channelBlockSize * 2;
            int ch4Offset = channelBlockSize * 3;

            unsafe
            {
                fixed (short* inputP = input)
                fixed (short* outputP = output)
                {
                    short* inputPtr = inputP;
                    short* outputPtr = outputP;
                    short* finishPtr = inputP + input.Length;
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

    public static void TwoChannels(ReadOnlySpan<short> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSize = output.Length / 2;

        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            var processingLength = Vector256<short>.Count * 2;
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSize;
            Vector256<sbyte> shuffleMask = Vector256.Create(
                0, 1, 4, 5, 8, 9, 12, 13, 2, 3, 6, 7, 10, 11, 14, 15,       // 128-bit lane
                0, 1, 4, 5, 8, 9, 12, 13, 2, 3, 6, 7, 10, 11, 14, 15);      // 128-bit lane
            Vector256<int> permuteMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);
            unsafe
            {
                fixed (short* inputP = input)
                fixed (short* outputP = output)
                {
                    short* inputPtr = inputP;
                    short* outputPtr = outputP;
                    short* finishPtr = inputP + input.Length;
                    while (inputPtr < finishPtr)
                    {
                        // Loads are non-temporal because the data is never used again. Store as normal.
                        var loaded1 = Vector256.LoadAlignedNonTemporal(inputPtr);
                        var loaded2 = Vector256.LoadAlignedNonTemporal(inputPtr + Vector256<short>.Count);
                        var shuffle1 = Avx2.Shuffle(loaded1.AsSByte(), shuffleMask);
                        var shuffle2 = Avx2.Shuffle(loaded2.AsSByte(), shuffleMask);
                        var permuted1 = Avx2.PermuteVar8x32(shuffle1.AsInt32(), permuteMask);
                        var permuted2 = Avx2.PermuteVar8x32(shuffle2.AsInt32(), permuteMask);
                        var channel1 = Avx2.Permute2x128(permuted1, permuted2, 0x20).AsInt16();
                        var channel2 = Avx2.Permute2x128(permuted1, permuted2, 0x31).AsInt16();
                        Vector256.StoreAligned(channel1, outputPtr);
                        Vector256.StoreAligned(channel2, outputPtr + ch2Offset);
                        inputPtr += processingLength;
                        outputPtr += Vector256<short>.Count;
                    }
                }
            }
        }
        else
        {
            var processingLength = 2;
            if (input.Length % processingLength != 0)
                throw new ArgumentException($"Input length must be multiple of {processingLength}");

            int ch2Offset = channelBlockSize;
            unsafe
            {
                fixed (short* inputP = input)
                fixed (short* outputP = output)
                {
                    short* inputPtr = inputP;
                    short* outputPtr = outputP;
                    short* finishPTr = inputP + input.Length;
                    while (inputPtr < finishPTr)
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
