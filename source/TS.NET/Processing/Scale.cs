using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET;

public static class Scale
{
    public static void I8toI16(ReadOnlySpan<sbyte> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        // The input values should be converted to shorts, scaling by 256.
        if (Avx2.IsSupported)       // Const after JIT/AOT
        {
            int n = input.Length;
            int i = 0;
            unsafe
            {
                fixed (sbyte* pInput = input)
                fixed (short* pOutput = output)
                {
                    while (i <= n - 16)
                    {
                        var widened = Avx2.ConvertToVector256Int16(pInput + i);
                        var shifted = Avx2.ShiftLeftLogical(widened, 8);
                        Avx.Store(pOutput + i, shifted);
                        i += 16;
                    }
                    while (i < n)
                    {
                        pOutput[i] = (short)(pInput[i] << 8);
                        i++;
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
