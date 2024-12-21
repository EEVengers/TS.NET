using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmarks
{
    //[SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser]
    //[CpuDiagnoser]
    //[InProcess]
    public class ShuffleI8Benchmark
    {
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> input = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> output = new sbyte[byteBufferSize];

        [GlobalSetup]
        public void Setup()
        {
            Waveforms.FourChannelCountSignedByte(input.Span);
        }

        [Benchmark(Description = "Four channel shuffle, run-length 1 (SIMD, optimised, 125 x 8MS)")]
        public void FourChannelsSimdOptimised1G()
        {
            for (int i = 0; i < 125; i++)
                ShuffleI8.FourChannels(input.Span, output.Span);
        }

        [Benchmark(Description = "Four channel shuffle, run-length 1 (no SIMD, optimised, 125 x 8MS)")]
        public void FourChannelsNoSimdOptimised1G()
        {
            for (int i = 0; i < 125; i++)
                ShuffleI8Benchmarks.FourChannelsNoSimd(input.Span, output.Span);
        }
    }
}
