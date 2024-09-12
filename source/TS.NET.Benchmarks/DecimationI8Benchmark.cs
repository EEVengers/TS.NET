using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    public class DecimationI8Benchmark
    {
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> input = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer = new sbyte[byteBufferSize];

        [GlobalSetup]
        public void Setup()
        {
            Waveforms.FourChannelCountSignedByte(input.Span);
        }

        [Benchmark(Description = "DecimationI8 (125 x 8MS)")]
        public void DecimationI8_Interval2()
        {
            for (int i = 0; i < 125; i++)
                DecimationI8.Process(input.Span, buffer.Span, 2);
        }
    }
}
