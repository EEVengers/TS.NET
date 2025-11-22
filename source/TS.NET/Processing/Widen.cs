using System.Runtime.InteropServices;
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

    public static void I8toF64(ReadOnlySpan<sbyte> input, Span<double> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");
        if ((input.Length % Vector256<sbyte>.Count) != 0)
            throw new ArgumentException("Input length must be a multiple of Vector256<sbyte>.Count");
        
        if (Vector256.IsHardwareAccelerated)       // Const after JIT/AOT
        {
            ref var inputR = ref MemoryMarshal.GetReference(input);
            ref var outputR = ref MemoryMarshal.GetReference(output);

            for (int i = 0; i < input.Length; i += Vector256<sbyte>.Count)
            {
                var inputV = Vector256.LoadUnsafe(ref inputR, (uint)i);
                var (i16L, i16U) = Vector256.Widen(inputV);
                var (i32LL, i32LU) = Vector256.Widen(i16L);
                var (i32UL, i32UU) = Vector256.Widen(i16U);
                var (i64LLL, i64LLU) = Vector256.Widen(i32LL);
                var (i64LUL, i64LUU) = Vector256.Widen(i32LU);
                var (i64ULL, i64ULU) = Vector256.Widen(i32UL);
                var (i64UUL, i64UUU) = Vector256.Widen(i32UU);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64LLL), ref outputR, (uint)i);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64LLU), ref outputR, (uint)i + 4);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64LUL), ref outputR, (uint)i + 8);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64LUU), ref outputR, (uint)i + 12);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64ULL), ref outputR, (uint)i + 16);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64ULU), ref outputR, (uint)i + 20);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64UUL), ref outputR, (uint)i + 24);
                Vector256.StoreUnsafe(Vector256.ConvertToDouble(i64UUU), ref outputR, (uint)i + 28);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            ref var inputR = ref MemoryMarshal.GetReference(input);
            ref var outputR = ref MemoryMarshal.GetReference(output);

            for (int i = 0; i < input.Length; i += Vector128<sbyte>.Count)
            {
                var inputV = Vector128.LoadUnsafe(ref inputR, (uint)i);
                var (i16L, i16U) = Vector128.Widen(inputV);
                var (i32LL, i32LU) = Vector128.Widen(i16L);
                var (i32UL, i32UU) = Vector128.Widen(i16U);
                var (i64LLL, i64LLU) = Vector128.Widen(i32LL);
                var (i64LUL, i64LUU) = Vector128.Widen(i32LU);
                var (i64ULL, i64ULU) = Vector128.Widen(i32UL);
                var (i64UUL, i64UUU) = Vector128.Widen(i32UU);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64LLL), ref outputR, (uint)i);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64LLU), ref outputR, (uint)i + 2);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64LUL), ref outputR, (uint)i + 4);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64LUU), ref outputR, (uint)i + 6);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64ULL), ref outputR, (uint)i + 8);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64ULU), ref outputR, (uint)i + 10);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64UUL), ref outputR, (uint)i + 12);
                Vector128.StoreUnsafe(Vector128.ConvertToDouble(i64UUU), ref outputR, (uint)i + 14);
            }
        }
        else
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = input[i];
            }
        }
    }
}
