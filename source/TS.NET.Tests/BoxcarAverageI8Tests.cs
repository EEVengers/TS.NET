using System;
using Xunit;

namespace TS.NET.Tests
{
    public class BoxcarAverageI8Tests
    {
        [Fact]
        public void BoxcarAverageI8_Iterations1_Samples32()
        {
            const int length = 32;
            ReadOnlySpan<sbyte> input = [1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 14, 15, 15, 16, 16];
            Span<short> buffer = new short[(length / 2)];

            var output = BoxcarAverageI8.I8ToI16(input, buffer, 1);

            Span<short> expectedOutput = [2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];

            for (int i = 0; i < expectedOutput.Length; i++)
            {
                Assert.Equal(expectedOutput[i], output[i]);
            }
        }

        //[Fact]
        //public void BoxcarAverageI8_Iterations2_Samples32()
        //{
        //    const int length = 32;
        //    ReadOnlySpan<sbyte> input = [1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 8];
        //    Span<short> buffer = new short[(length / 2)];

        //    var output = BoxcarAverageI8.I8ToI16(input, buffer, 2);

        //    Span<short> expectedOutput = [4, 8, 12, 16, 20, 24, 28, 32];

        //    for (int i = 0; i < expectedOutput.Length; i++)
        //    {
        //        Assert.Equal(expectedOutput[i], output[i]);
        //    }
        //}
    }
}
