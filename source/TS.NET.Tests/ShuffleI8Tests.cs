using System;
using Xunit;

namespace TS.NET.Tests
{
    public class ShuffleI8Tests
    {
        [Fact]
        public void ShuffleI8_FourChannels_Samples64()
        {
            const int length = 64;
            ReadOnlySpan<sbyte> input = [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.FourChannels(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleI8_FourChannels_Samples128()
        {
            const int length = 128;
            Span<sbyte> input = new sbyte[length];
            for (int i = 0; i < length; i += 4)
            {
                input[i] = 1;
                input[i + 1] = 2;
                input[i + 2] = 3;
                input[i + 3] = 4;
            }
            Span<sbyte> output = new sbyte[length];

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
        }

        [Fact]
        public void ShuffleI8_FourChannels_Samples8388608()
        {
            const int length = 1 << 23;     // 8388608 bytes
            Span<sbyte> input = new sbyte[length];
            for (int i = 0; i < length; i += 4)
            {
                input[i] = 1;
                input[i + 1] = 2;
                input[i + 2] = 3;
                input[i + 3] = 4;
            }
            Span<sbyte> output = new sbyte[length];

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
        }

        [Fact]
        public void ShuffleI8_TwoChannels_Samples128()
        {
            const int length = 128;
            Span<sbyte> input = new sbyte[length];
            for (int i = 0; i < length; i += 2)
            {
                input[i] = 1;
                input[i + 1] = 2;
            }
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.TwoChannels(input, output);

            Span<sbyte> expectedOutput = new sbyte[length];
            var runLength = length / 2;
            expectedOutput.Slice(runLength * 0, runLength).Fill(1);
            expectedOutput.Slice(runLength * 1, runLength).Fill(2);

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleI8_TwoChannels_Samples8388608()
        {
            const int length = 1 << 23;     // 8388608 bytes
            Span<sbyte> input = new sbyte[length];
            for (int i = 0; i < length; i += 2)
            {
                input[i] = 1;
                input[i + 1] = 2;
            }
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.TwoChannels(input, output);

            Span<sbyte> expectedOutput = new sbyte[length];
            var runLength = length / 2;
            expectedOutput.Slice(runLength * 0, runLength).Fill(1);
            expectedOutput.Slice(runLength * 1, runLength).Fill(2);

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }
    }
}
