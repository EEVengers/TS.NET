using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace TS.NET.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    //[CpuDiagnoser]
    //[InProcess]
    //[HardwareCounters(HardwareCounter.TotalIssues)]
    public class RisingEdgeTriggerI8Benchmark
    {
        private const int samplingRate = 1000000000;
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> bufferDC0 = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> bufferDC50 = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1MHz = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1KHz = new sbyte[byteBufferSize];
        private readonly Memory<uint> captureEndIndicesU64 = new uint[byteBufferSize / 64];
        private readonly RisingEdgeTriggerI8 trigger = new();

        [GlobalSetup]
        public void Setup()
        {
            bufferDC0.Span.Fill(50);
            bufferDC50.Span.Fill(50);
            Waveforms.SineI8(buffer1MHz.Span, samplingRate, 1000000);
            Waveforms.SineI8(buffer1KHz.Span, samplingRate, 1000);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M")]
        public void RisingEdge1()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC0.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M")]
        public void RisingEdge2()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC50.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M")]
        public void RisingEdge3()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1KHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        // 0.18 CPU cycles per sample
        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M")]
        public void RisingEdge4()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1MHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M")]
        public void RisingEdge5()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC0.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M")]
        public void RisingEdge6()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC50.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M")]
        public void RisingEdge7()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1KHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Rising edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M")]
        public void RisingEdge8()
        {
            trigger.SetVertical(0, 10);
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1MHz.Span, captureEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }
    }
}
