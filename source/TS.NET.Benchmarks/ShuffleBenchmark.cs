using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmark
{
    [SimpleJob(RuntimeMoniker.Net80)]
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

        //[Benchmark(Description = "Four channel shuffle [run length 1, baseline] (125 x 8MS)")]
        //public void FourChannels()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength1(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 1, variant A] (125 x 8MS)")]
        //public void FourChannelsVariantA()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength1VariantA(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 1, variant B] (125 x 8MS)")]
        //public void FourChannelsVariantB()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength1VariantB(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 1, variant C] (125 x 8MS)")]
        //public void FourChannelsVariantC()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength1VariantC(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [no SIMD] (125 x 8MS)")]
        //public void FourChannelsNoSimd()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsNoSimd(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 4] (125 x 8MS)")]
        //public void FourChannelsRunLength4()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength4(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 8] (125 x 8MS)")]
        //public void FourChannelsRunLength8()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength8(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 32] (125 x 8MS)")]
        //public void FourChannelsRunLength32()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength32(input.Span, output.Span);
        //}

        //[Benchmark(Description = "Four channel shuffle [run length 32, no SIMD] (125 x 8MS)")]
        //public void FourChannelsRunLength32NoSimd()
        //{
        //    for (int i = 0; i < 125; i++)
        //        Shuffle.FourChannelsRunLength32NoSimd(input.Span, output.Span);
        //}

        [Benchmark(Description = "Two channel shuffle (125 x 8MS)")]
        public void TwoChannels()
        {
            for (int i = 0; i < 125; i++)
                Shuffle.TwoChannels(input.Span, output.Span);
        }
    }
}
