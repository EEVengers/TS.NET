using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using System;

namespace TS.NET.Benchmark
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    //[CpuDiagnoser]
    //[InProcess]
    //[HardwareCounters(HardwareCounter.TotalIssues)]
    public class AnyEdgeTriggerBenchmark
    {
        private const int samplingRate = 1000000000;
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> bufferDC0 = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> bufferDC50 = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1MHz = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1KHz = new sbyte[byteBufferSize];
        private readonly Memory<uint> captureEndIndicesU64 = new uint[byteBufferSize / 64];
        private readonly AnyEdgeTriggerI8 trigger = new();
        private readonly ulong ChannelLength = 1000;

        [GlobalSetup]
        public void Setup()
        {
            bufferDC0.Span.Fill(50);
            bufferDC50.Span.Fill(50);
            Waveforms.SineI8(buffer1MHz.Span, samplingRate, 1000000);
            Waveforms.SineI8(buffer1KHz.Span, samplingRate, 1000);
        }

        //[Benchmark(Description = "Any edge with hysteresis (10 counts) & holdoff (1us) and no SIMD, 1KHz sine (125 x 8MS)")]
        //public void AnyEdge2()
        //{
        //    for (int i = 0; i < 125; i++)
        //        trigger.AnyEdge(buffer1KHz.Span, triggerBufferU64.Span);
        //}

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M")]
        public void AnyEdge1()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: bufferDC0.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M")]
        public void AnyEdge2()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: bufferDC50.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M")]
        public void AnyEdge3()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: buffer1KHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        // 0.18 CPU cycles per sample
        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M")]
        public void AnyEdge4()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: buffer1MHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M")]
        public void AnyEdge5()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: bufferDC0.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M")]
        public void AnyEdge6()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: bufferDC50.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M")]
        public void AnyEdge7()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: buffer1KHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M")]
        public void AnyEdge8()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.ProcessSimd(input: buffer1MHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }
    }
}
