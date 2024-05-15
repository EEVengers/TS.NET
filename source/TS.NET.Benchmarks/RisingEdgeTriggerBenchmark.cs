using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmark
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    //[CpuDiagnoser]
    //[InProcess]
    //[HardwareCounters(HardwareCounter.TotalIssues)]
    public class RisingEdgeTriggerBenchmark
    {
        private const int samplingRate = 1000000000;
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> buffer1MHz = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1KHz = new sbyte[byteBufferSize];
        private readonly Memory<uint> triggerIndicesU64 = new uint[byteBufferSize / 64];
        private readonly Memory<uint> holdoffIndicesU64 = new uint[byteBufferSize / 64];
        private readonly RisingEdgeTriggerI8 trigger = new(0, -10, 1000);
        private readonly ulong ChannelLength = 1000;

        [GlobalSetup]
        public void Setup()
        {
            Waveforms.SineI8(buffer1MHz.Span, samplingRate, 1000000);
            Waveforms.SineI8(buffer1KHz.Span, samplingRate, 1000);
        }

        //[Benchmark(Description = "Rising edge with hysteresis (10 counts) & holdoff (1us) and no SIMD, 1KHz sine (125 x 8MS)")]
        //public void RisingEdge2()
        //{
        //    for (int i = 0; i < 125; i++)
        //        trigger.RisingEdge(buffer1KHz.Span, triggerBufferU64.Span);
        //}

        [Benchmark(Description = "Rising edge with hysteresis (10 counts), holdoff (1us) & SIMD, 1KHz sine (125 x 8MS = 1GS)")]
        public void RisingEdge1()
        {
            trigger.Reset(0, -10, ChannelLength);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: buffer1KHz.Span, triggerIndices: triggerIndicesU64.Span, out uint triggerCount, holdoffEndIndices: holdoffIndicesU64.Span, out uint holdoffEndCount);
        }

        // 0.18 CPU cycles per sample
        [Benchmark(Description = "Rising edge with hysteresis (10 counts), holdoff (1us) & SIMD, 1MHz sine (125 x 8MS = 1GS)")]
        public void RisingEdge2()
        {
            trigger.Reset(0, -10, ChannelLength);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: buffer1KHz.Span, triggerIndices: triggerIndicesU64.Span, out uint triggerCount, holdoffEndIndices: holdoffIndicesU64.Span, out uint holdoffEndCount);
        }

        //[Benchmark(Description = "Rising edge with hysteresis (10 counts), holdoff (1ms) & SIMD, 1KHz sine (125 x 8MS)")]
        //public void RisingEdge3()
        //{
        //    trigger.Reset(200, 190, 1000000);
        //    for (int i = 0; i < 125; i++)
        //        trigger.ProcessSimd(buffer1KHz.Span, triggerBufferU64.Span);
        //}

        //[Benchmark(Description = "Rising edge with hysteresis (10 counts), holdoff (1ms) & SIMD, 1MHz sine (125 x 8MS)")]
        //public void RisingEdge4()
        //{
        //    trigger.Reset(200, 190, 1000000);
        //    for (int i = 0; i < 125; i++)
        //        trigger.ProcessSimd(buffer1MHz.Span, triggerBufferU64.Span);
        //}
    }
}
