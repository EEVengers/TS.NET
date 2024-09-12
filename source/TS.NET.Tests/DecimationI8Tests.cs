using System;
using Xunit;

namespace TS.NET.Tests
{
    public class DecimationI8Tests
    {
        [Fact]
        public void DecimationI8_Interval2_Samples32()
        {
            const int length = 32;
            ReadOnlySpan<sbyte> input = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
            Span<sbyte> buffer = new sbyte[(length / 2) + 32];

            var output = DecimationI8.Process(input, buffer, 2);

            Span<sbyte> expectedOutput = [1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31];

            for (int i = 0; i < expectedOutput.Length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        [Fact]
        public void DecimationI8_Interval4_Samples32()
        {
            const int length = 32;
            ReadOnlySpan<sbyte> input = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
            Span<sbyte> buffer = new sbyte[(length / 2) + 32];

            var output = DecimationI8.Process(input, buffer, 4);

            Span<sbyte> expectedOutput = [1, 5, 9, 13, 17, 21, 25, 29];

            for (int i = 0; i < expectedOutput.Length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }
    }
}
