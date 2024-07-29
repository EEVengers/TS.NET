using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using Xunit;

namespace TS.NET.Tests
{
    public class ShuffleTests
    {
        [Fact]
        public void ShuffleFourChannels_RunLength1_Samples32()
        {
            ReadOnlySpan<sbyte> input = [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];
            Span<sbyte> output = new sbyte[32];

            Shuffle.FourChannels(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < 32; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleFourChannels_RunLength1_Samples64()
        {
            ReadOnlySpan<sbyte> input = [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];
            Span<sbyte> output = new sbyte[64];

            Shuffle.FourChannels(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < 32; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleFourChannels_RunLength8_Samples64()
        {
            ReadOnlySpan<sbyte> input = [1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4];
            Span<sbyte> output = new sbyte[64];

            Shuffle.FourChannelsRunLength8(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < 32; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleFourChannels_RunLength32_Samples1024()
        {
            Span<sbyte> input = new sbyte[1024];
            for (int i = 0; i < input.Length;)
            {
                for (int n = i; n < i + 32; n++)
                {
                    input[n] = 1;
                }
                i += 32;
                for (int n = i; n < i + 32; n++)
                {
                    input[n] = 2;
                }
                i += 32;
                for (int n = i; n < i + 32; n++)
                {
                    input[n] = 3;
                }
                i += 32;
                for (int n = i; n < i + 32; n++)
                {
                    input[n] = 4;
                }
                i += 32;
            }
            Span<sbyte> output = new sbyte[1024];
            Shuffle.FourChannelsRunLength32(input, output);

            for (int i = 0; i < 256; i++)
            {
                Assert.Equal(1, output[i]);
            }
            for (int i = 256; i < 512; i++)
            {
                Assert.Equal(2, output[i]);
            }
            for (int i = 512; i < 768; i++)
            {
                Assert.Equal(3, output[i]);
            }
            for (int i = 768; i < 1024; i++)
            {
                Assert.Equal(4, output[i]);
            }
        }
    }
}
