using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public static class Widen
{
    public static void I8toI16_NoScale(ReadOnlySpan<sbyte> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");
        if ((input.Length % Vector256<short>.Count) != 0)
            throw new ArgumentException("Input length must be a multiple of Vector256<short>.Count");

        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            int i = 0;
            unsafe
            {
                fixed (sbyte* pInput = input)
                fixed (short* pOutput = output)
                {
                    while (i < input.Length)
                    {
                        var widened = Avx2.ConvertToVector256Int16(pInput + i);
                        Avx.Store(pOutput + i, widened);
                        i += Vector256<short>.Count;
                    }
                }
            }
        }
        else if (Ssse3.IsSupported)
        {
            throw new NotImplementedException();
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            throw new NotImplementedException();
        }
        else
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (short)(input[i]);
            }
        }
    }

    public static void I8toI16_Scale(ReadOnlySpan<sbyte> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");
        if ((input.Length % Vector256<short>.Count) != 0)
            throw new ArgumentException("Input length must be a multiple of Vector256<short>.Count");

        // The input values should be converted to shorts, scaling by 256.
        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            int i = 0;
            unsafe
            {
                fixed (sbyte* pInput = input)
                fixed (short* pOutput = output)
                {
                    while (i < input.Length)
                    {
                        var widened = Avx2.ConvertToVector256Int16(pInput + i);
                        var shifted = Avx2.ShiftLeftLogical(widened, 8);
                        Avx.Store(pOutput + i, shifted);
                        i += Vector256<short>.Count;
                    }
                }
            }
        }
        else if (Ssse3.IsSupported)
        {
            throw new NotImplementedException();
        }
        else if (AdvSimd.Arm64.IsSupported)
        {
            throw new NotImplementedException();
        }
        else
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (short)(input[i] << 8);
            }
        }
    }
}
