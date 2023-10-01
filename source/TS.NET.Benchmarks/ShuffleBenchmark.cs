﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmark
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [MemoryDiagnoser]
    //[CpuDiagnoser]
    //[InProcess]
    public class ShuffleBenchmark
    {
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> input = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> output = new sbyte[byteBufferSize];

        [GlobalSetup]
        public void Setup()
        {
            Waveforms.FourChannelCountSignedByte(input.Span);
        }

        [Benchmark(Description = "Four channel shuffle (125 x 8MS)")]   // 0.40 CPU cycles per sample
        public void FourChannels()
        {
            for (int i = 0; i < 125; i++)
                Shuffle.FourChannels(input.Span, output.Span);
        }

        [Benchmark(Description = "Two channel shuffle (125 x 8MS)")]    // 0.32 CPU cycles per sample
        public void TwoChannels()
        {
            for (int i = 0; i < 125; i++)
                Shuffle.TwoChannels(input.Span, output.Span);
        }
    }
}
