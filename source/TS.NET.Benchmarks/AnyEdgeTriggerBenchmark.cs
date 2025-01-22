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
    public class AnyEdgeTriggerBenchmark
    {
        private const int samplingRate = 1000000000;
        private const int byteBufferSize = 8000000;
        private readonly Memory<sbyte> bufferDC0 = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> bufferDC50 = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1MHz = new sbyte[byteBufferSize];
        private readonly Memory<sbyte> buffer1KHz = new sbyte[byteBufferSize];
        private readonly Memory<int> captureEndIndicesU64 = new int[byteBufferSize / 64];
        private readonly AnyEdgeTriggerI8 trigger = new(new EdgeTriggerParameters());

        [GlobalSetup]
        public void Setup()
        {
            bufferDC0.Span.Fill(50);
            bufferDC50.Span.Fill(50);
            Waveforms.SineI8(buffer1MHz.Span, samplingRate, 1000000);
            Waveforms.SineI8(buffer1KHz.Span, samplingRate, 1000);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: DC (0), run length: 125 x 8M")]
        public void AnyEdge1()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC0.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: DC (50), run length: 125 x 8M")]
        public void AnyEdge2()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC50.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: 1KHz sine, run length: 125 x 8M")]
        public void AnyEdge3()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1KHz.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        // 0.18 CPU cycles per sample
        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1us, signal: 1MHz sine, run length: 125 x 8M")]
        public void AnyEdge4()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1MHz.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: DC (0), run length: 125 x 8M")]
        public void AnyEdge5()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC0.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: DC (50), run length: 125 x 8M")]
        public void AnyEdge6()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: bufferDC50.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: 1KHz sine, run length: 125 x 8M")]
        public void AnyEdge7()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1KHz.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }

        [Benchmark(Description = "Any edge (hysteresis: 10), width: 1ms, signal: 1MHz sine, run length: 125 x 8M")]
        public void AnyEdge8()
        {
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Any });
            trigger.SetHorizontal(1000000, 0, 0);
            for (int i = 0; i < 125; i++)
                trigger.Process(input: buffer1MHz.Span, windowEndIndices: captureEndIndicesU64.Span, out int captureEndCount);
        }
    }
}
