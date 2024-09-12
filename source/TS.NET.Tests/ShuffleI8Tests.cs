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
        public void ShuffleI8_FourChannels_RunLength1_VariantA_Samples128()
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

            ShuffleI8.FourChannelsRunLength1VariantA(input, output);

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
        public void ShuffleI8_FourChannels_RunLength1_VariantB_Samples128()
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

            ShuffleI8.FourChannelsRunLength1VariantB(input, output);

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
        public void ShuffleI8_FourChannels_RunLength1_VariantC_Samples128()
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

            ShuffleI8.FourChannelsRunLength1VariantC(input, output);

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
        public void ShuffleI8_FourChannelsNoSimd_RunLength1_Samples64()
        {
            const int length = 64;
            ReadOnlySpan<sbyte> input = [1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4];
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.FourChannelsNoSimd(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleI8_FourChannels_RunLength4_Samples64()
        {
            const int length = 64;
            ReadOnlySpan<sbyte> input = [1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4];
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.FourChannelsRunLength4(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleI8_FourChannels_RunLength8_Samples64()
        {
            const int length = 64;
            ReadOnlySpan<sbyte> input = [1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4];
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.FourChannelsRunLength8(input, output);

            Span<sbyte> expectedOutput = [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4];

            for (int i = 0; i < length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void ShuffleI8_FourChannels_RunLength32_Samples1024()
        {
            const int length = 1024;
            Span<sbyte> input = new sbyte[length];
            for (int i = 0; i < length;)
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
            Span<sbyte> output = new sbyte[length];
            ShuffleI8.FourChannelsRunLength32(input, output);

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

        [Fact]
        public void ShuffleI8_TwoChannels_Samples64()
        {
            const int length = 64;
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

        [Fact]
        public void ShuffleI8_TwoChannels_RunLength1_VariantA_Samples64()
        {
            const int length = 64;
            Span<sbyte> input = new sbyte[length];
            for (int i = 0; i < length; i += 2)
            {
                input[i] = 1;
                input[i + 1] = 2;
            }
            Span<sbyte> output = new sbyte[length];

            ShuffleI8.TwoChannelsRunLength1VariantA(input, output);

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
