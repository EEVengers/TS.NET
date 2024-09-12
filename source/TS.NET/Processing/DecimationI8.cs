using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace TS.NET
{
    // Temporary class to get lower sample rates.
    // The proper way to get lower sample rates is:
    // 1. Change sample rate in hardware
    // 2. Use BoxcarAverageI8 to gain SNR
    public static class DecimationI8
    {
        public static Span<sbyte> Process(ReadOnlySpan<sbyte> input, Span<sbyte> buffer, byte interval)
        {
            if (input.Length % 32 != 0)
                throw new ArgumentException("Input length must be multiple of 16");
            if (buffer.Length < ((input.Length / 2) + 32))
                throw new ArgumentException($"Buffer has incorrect length, should be at least {(input.Length / 2) + 32}");

            switch (interval)
            {
                case 2:
                    Vector256<sbyte> shuffleMask = Vector256.Create(0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15, 0, 2, 4, 6, 8, 10, 12, 14, 1, 3, 5, 7, 9, 11, 13, 15).AsSByte();
                    unsafe
                    {
                        fixed (sbyte* inputP = input)
                        fixed (sbyte* outputP = buffer)
                        {
                            sbyte* inputPtr = inputP;
                            sbyte* outputPtr = outputP;
                            sbyte* finishPtr = inputP + input.Length;
                            while (inputPtr < finishPtr)
                            {
                                var inputVector = Avx.LoadVector256(inputPtr);
                                var shuffleVector = Avx2.Shuffle(inputVector, shuffleMask);
                                Avx.Store(outputPtr, shuffleVector);
                                outputPtr += 8;
                                var permutedVector = Avx2.Permute2x128(shuffleVector, shuffleVector, 1);
                                Avx.Store(outputPtr, permutedVector);
                                outputPtr += 8;
                                inputPtr += 32;
                            }
                        }
                    }
                    return buffer.Slice(0, input.Length / 2);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
