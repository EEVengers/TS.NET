using System;
using System.Runtime.InteropServices;
using Xunit;

namespace TS.NET.Tests;

public class ShuffleI8Tests
{
    [Fact]
    public unsafe void ShuffleI8_FourChannels_Samples128()
    {
        const int length = 128;
        var inputP = NativeMemory.AlignedAlloc(length, 32);
        var input = new Span<sbyte>((sbyte*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length, 32);
        var output = new Span<sbyte>((sbyte*)outputP, length);

        for (int i = 0; i < length; i += 4)
        {
            input[i] = 1;
            input[i + 1] = 2;
            input[i + 2] = 3;
            input[i + 3] = 4;
        }

        ShuffleI8.FourChannels(input, output);

        Span<sbyte> expectedOutput = new sbyte[length];
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
    public unsafe void ShuffleI8_FourChannels_Samples128_Alt()
    {
        const int length = 128;
        var inputP = NativeMemory.AlignedAlloc(length, 32);
        var input = new Span<sbyte>((sbyte*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length, 32);
        var output = new Span<sbyte>((sbyte*)outputP, length);

        int n = 0;
        for (int i = 0; i < length; i += 4)
        {
            input[i] = (sbyte)n;
            input[i + 1] = (sbyte)(n + 32);
            input[i + 2] = (sbyte)(n + 64);
            input[i + 3] = (sbyte)(n + 96);
            n++;
        }

        ShuffleI8.FourChannels(input, output);

        for (int i = 0; i < length; i++)
        {
            Assert.Equal(i, output[i]);
        }

        NativeMemory.AlignedFree(inputP);
        NativeMemory.AlignedFree(outputP);
    }

    [Fact]
    public unsafe void ShuffleI8_FourChannels_Samples8388608()
    {
        const int length = 1 << 23;     // 8388608 bytes
        var inputP = NativeMemory.AlignedAlloc(length, 32);
        var input = new Span<sbyte>((sbyte*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length, 32);
        var output = new Span<sbyte>((sbyte*)outputP, length);

        for (int i = 0; i < length; i += 4)
        {
            input[i] = 1;
            input[i + 1] = 2;
            input[i + 2] = 3;
            input[i + 3] = 4;
        }

        ShuffleI8.FourChannels(input, output);

        Span<sbyte> expectedOutput = new sbyte[length];
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
    public unsafe void ShuffleI8_TwoChannels_Samples128()
    {
        const int length = 128;
        var inputP = NativeMemory.AlignedAlloc(length, 32);
        var input = new Span<sbyte>((sbyte*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length, 32);
        var output = new Span<sbyte>((sbyte*)outputP, length);

        for (int i = 0; i < length; i += 2)
        {
            input[i] = 1;
            input[i + 1] = 2;
        }

        ShuffleI8.TwoChannels(input, output);

        Span<sbyte> expectedOutput = new sbyte[length];
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

    [Fact]
    public unsafe void ShuffleI8_TwoChannels_Samples128_Alt()
    {
        const int length = 128;
        var inputP = NativeMemory.AlignedAlloc(length, 32);
        var input = new Span<sbyte>((sbyte*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length, 32);
        var output = new Span<sbyte>((sbyte*)outputP, length);

        int n = 0;
        for (int i = 0; i < length; i += 2)
        {
            input[i] = (sbyte)n;
            input[i + 1] = (sbyte)(n + 64);
            n++;
        }

        ShuffleI8.TwoChannels(input, output);

        for (int i = 0; i < length; i++)
        {
            Assert.Equal(i, output[i]);
        }

        NativeMemory.AlignedFree(inputP);
        NativeMemory.AlignedFree(outputP);
    }

    [Fact]
    public unsafe void ShuffleI8_TwoChannels_Samples8388608()
    {
        const int length = 1 << 23;     // 8388608 bytes
        var inputP = NativeMemory.AlignedAlloc(length, 32);
        var input = new Span<sbyte>((sbyte*)inputP, length);
        var outputP = NativeMemory.AlignedAlloc(length, 32);
        var output = new Span<sbyte>((sbyte*)outputP, length);

        for (int i = 0; i < length; i += 2)
        {
            input[i] = 1;
            input[i + 1] = 2;
        }

        ShuffleI8.TwoChannels(input, output);

        Span<sbyte> expectedOutput = new sbyte[length];
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
