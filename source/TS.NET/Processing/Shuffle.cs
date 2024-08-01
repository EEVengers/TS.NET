using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace TS.NET;

public class Shuffle
{
    public static void FourChannels(ReadOnlySpan<sbyte> input, Span<sbyte> output)
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

    public static void TwoChannels(ReadOnlySpan<sbyte> input, Span<sbyte> output)
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
                    Vector256<ulong> shuffledVector = Avx2.PermuteVar8x32(Avx2.Shuffle(Avx.LoadVector256(inputPtr), shuffleMask).AsInt32(), permuteMask).AsUInt64();
                    Avx2.MaskStore(outputPtr, storeCh1Mask, shuffledVector);
                    Avx2.MaskStore(outputPtr + ch2Offset, storeCh2Mask, shuffledVector);
                    inputPtr += 32;
                    outputPtr += 2;
                }
            }
        }
    }

    // For benchmarking

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
}