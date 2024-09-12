using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class BoxcarAverageI8Benchmark
    {
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> input = new sbyte[byteBufferSize];
        private readonly Memory<short> bufferI16 = new short[byteBufferSize / 2];
        private readonly Memory<int> bufferI32 = new int[byteBufferSize / 2];

        [GlobalSetup]
        public void Setup()
        {
            Waveforms.Oversampling_1Channel_2Avg(input.Span);
        }

        [Benchmark(Description = "BoxcarAverageI8, iterations 2 (1GS -> 500MS)", Baseline = true)]  // x CPU cycles per sample
        public void I16_2()
        {
            for (int i = 0; i < 125; i++)
                BoxcarAverageI8.I8ToI16(input.Span, bufferI16.Span, 1);
        }

        [Benchmark(Description = "BoxcarAverageI8, iterations 4 (1GS -> 250MS)")]  // x CPU cycles per sample
        public void I16_4()
        {
            for (int i = 0; i < 125; i++)
                BoxcarAverageI8.I8ToI16(input.Span, bufferI16.Span, 2);
        }

        //[Benchmark(Description = "I16: Sum by 8 (1GS -> 125MS)")]  // x CPU cycles per sample
        //public void I16_8()
        //{
        //    for (int i = 0; i < 125; i++)
        //        BoxcarAverageI8.I8ToI16(input.Span, bufferI16.Span, 3);
        //}

        //[Benchmark(Description = "I16: Sum by 16 (1GS -> 62.5MS)")]  // x CPU cycles per sample
        //public void I16_16()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Sum.U8ToI16(input.Span, bufferI16.Span, 4);
        //}

        //[Benchmark(Description = "I16: Sum by 32 (1GS -> 31.25MS)")]  // x CPU cycles per sample
        //public void I16_32()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Sum.U8ToI16(input.Span, bufferI16.Span, 5);
        //}

        //[Benchmark(Description = "I16: Sum by 64 (1GS -> 15.625MS)")]  // x CPU cycles per sample
        //public void I16_64()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Sum.U8ToI16(input.Span, bufferI16.Span, 6);
        //}

        //[Benchmark(Description = "I16: Sum by 128 (1GS -> 7.8125MS)")]  // x CPU cycles per sample
        //public void I16_128()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Sum.U8ToI16(input.Span, bufferI16.Span, 7);
        //}
    }
}
