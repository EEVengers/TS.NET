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

        // [Benchmark(Description = "Four channel shuffle, run-length 1 (SIMD, baseline, 125 x 8MS)")]
        // public void FourChannels()
        // {
        //     for (int i = 0; i < 125; i++)
        //         ShuffleI8Benchmarks.FourChannelsRunLength1(input.Span, output.Span);
        // }

        [Benchmark(Description = "Four channel shuffle, run-length 1 (SIMD, optimised, 125 x 8MS)")]
        public void FourChannelsSimdOptimised()
        {
            for (int i = 0; i < 125; i++)
                ShuffleI8.FourChannels(input.Span, output.Span);
        }

        // [Benchmark(Description = "Four channel shuffle, run-length 32 (SIMD, baseline, 125 x 8MS)")]
        // public void FourChannelsRunLength32()
        // {
        //     for (int i = 0; i < 125; i++)
        //         ShuffleI8Benchmarks.FourChannelsRunLength32(input.Span, output.Span);
        // }

        [Benchmark(Description = "Four channel shuffle, run-length 1 (no SIMD, optimised, 125 x 8MS)")]
        public void FourChannelsNoSimdOptimised()
        {
            for (int i = 0; i < 125; i++)
                ShuffleI8Benchmarks.FourChannelsNoSimd(input.Span, output.Span);
        }

        [Benchmark(Description = "Four channel shuffle, run-length 32 (SIMD, optimised, 125 x 8MS)")]
        public void FourChannelsRunLength32VariantA()
        {
            for (int i = 0; i < 125; i++)
                ShuffleI8Benchmarks.FourChannelsRunLength32VariantA(input.Span, output.Span);
        }

        // [Benchmark(Description = "Four channel shuffle, run-length 32 (no SIMD, baseline, 125 x 8MS)")]
        // public void FourChannelsRunLength32NoSimd()
        // {
        //     for (int i = 0; i < 125; i++)
        //         ShuffleI8Benchmarks.FourChannelsRunLength32NoSimd(input.Span, output.Span);
        // }

        [Benchmark(Description = "Four channel shuffle, run-length 32 (no SIMD, optimised, 125 x 8MS)")]
        public void FourChannelsRunLength32NoSimd2()
        {
            for (int i = 0; i < 125; i++)
                ShuffleI8Benchmarks.FourChannelsRunLength32NoSimd3(input.Span, output.Span);
        }
    }
}
