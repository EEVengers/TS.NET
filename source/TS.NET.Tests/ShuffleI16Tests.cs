using System;
using System.Runtime.InteropServices;
using Xunit;

namespace TS.NET.Tests;

public class ShuffleI16Tests
{
    [Fact]
    public unsafe void ShuffleI16_FourChannels_Samples128()
    {
        const int length = 128;
        var inputP = NativeMemory.AlignedAlloc(length * sizeof(short), 32);
        var input = new Span<short>((short*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length * sizeof(short), 32);
        var output = new Span<short>((short*)outputP, length);

        for (int i = 0; i < length; i += 4)
        {
            input[i] = 1;
            input[i + 1] = 2;
            input[i + 2] = 3;
            input[i + 3] = 4;
        }

        ShuffleI16.FourChannels(input, output);

        Span<short> expectedOutput = new short[length];
        var runLength = length / 4;
        expectedOutput.Slice(runLength * 0, runLength).Fill(1);
        expectedOutput.Slice(runLength * 1, runLength).Fill(2);
        expectedOutput.Slice(runLength * 2, runLength).Fill(3);
        expectedOutput.Slice(runLength * 3, runLength).Fill(4);

        for (int i = 0; i < length; i++)
        {
            Assert.Equal(expectedOutput[i], output[i]);
        }

        NativeMemory.AlignedFree(inputP);
        NativeMemory.AlignedFree(outputP);
    }

    [Fact]
    public unsafe void ShuffleI16_TwoChannels_Samples128()
    {
        const int length = 128;
        var inputP = NativeMemory.AlignedAlloc(length * sizeof(short), 32);
        var input = new Span<short>((short*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length * sizeof(short), 32);
        var output = new Span<short>((short*)outputP, length);

        for (int i = 0; i < length; i += 2)
        {
            input[i] = 1;
            input[i + 1] = 2;
        }

        ShuffleI16.TwoChannels(input, output);

        Span<short> expectedOutput = new short[length];
        var runLength = length / 2;
        expectedOutput.Slice(runLength * 0, runLength).Fill(1);
        expectedOutput.Slice(runLength * 1, runLength).Fill(2);

        for (int i = 0; i < length; i++)
        {
            Assert.Equal(expectedOutput[i], output[i]);
        }

        NativeMemory.AlignedFree(inputP);
        NativeMemory.AlignedFree(outputP);
    }
}