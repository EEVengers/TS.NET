namespace TS.NET;

public static class ShuffleI16
{
    public static void FourChannels(ReadOnlySpan<short> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSize = output.Length / 4;

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
                    outputPtr[0 + channelBlockSize] = inputPtr[1];
                    outputPtr[0 + channelBlockSize] = inputPtr[2];
                    outputPtr[0 + channelBlockSize] = inputPtr[3];
                    inputPtr += processingLength;
                    outputPtr += 1;
                }
            }
        }
    }

    public static void TwoChannels(ReadOnlySpan<short> input, Span<short> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("Array lengths must match");

        int channelBlockSize = output.Length / 2;

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
