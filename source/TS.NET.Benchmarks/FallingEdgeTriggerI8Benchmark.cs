using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TS.NET.Benchmarks
{
    public unsafe class FallingEdgeTriggerI8Benchmark
    {
        private const int byteBufferSize = 1 << 23;
        private void* inputP_DC;
        private void* inputP_500MHz;
        private void* inputP_50percent;

        private readonly Memory<uint> captureEndIndicesU64 = new uint[byteBufferSize / 64];
        private readonly FallingEdgeTriggerI8 trigger = new(new EdgeTriggerParameters());

        [GlobalSetup]
        public void Setup()
        {
            inputP_DC = NativeMemory.AlignedAlloc(byteBufferSize, 32);           
            var input_DC = new Span<sbyte>((sbyte*)inputP_DC, byteBufferSize); 
            input_DC.Fill(sbyte.MinValue);

            inputP_500MHz = NativeMemory.AlignedAlloc(byteBufferSize, 32);
            var input_500MHz = new Span<sbyte>((sbyte*)inputP_500MHz, byteBufferSize); 
            input_500MHz.Fill(sbyte.MaxValue);
            for(int i = 0; i < input_500MHz.Length; i+=2)
            {
                input_500MHz[i] = sbyte.MinValue;
            }

            inputP_50percent = NativeMemory.AlignedAlloc(byteBufferSize, 32);
            var input_50percent = new Span<sbyte>((sbyte*)inputP_50percent, byteBufferSize); 
            input_50percent.Fill(sbyte.MaxValue);
            for(int i = 0; i < input_50percent.Length; i+=2097152)
            {
                input_50percent[i] = sbyte.MinValue;
            }
        }

        [Benchmark(Description = "Falling edge (hysteresis: 10), width: 1ms, signal: DC (127), 1006632960 samples")]
        public void FallingEdge1()
        {
            var input = new Span<sbyte>((sbyte*)inputP_DC, byteBufferSize); 
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Falling });
            trigger.SetHorizontal(1000000, 0, 0);
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                trigger.Process(input: input, windowEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Falling edge (hysteresis: 10), width: 1ms, signal: 476.8Hz, 1006632960 samples")]
        public void FallingEdge2()
        {
            var input = new Span<sbyte>((sbyte*)inputP_50percent, byteBufferSize); 
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Falling });
            trigger.SetHorizontal(1000000, 0, 0);
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                trigger.Process(input: input, windowEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [Benchmark(Description = "Falling edge (hysteresis: 10), width: 1ms, signal: 500MHz, 1006632960 samples")]
        public void FallingEdge3()
        {
            var input = new Span<sbyte>((sbyte*)inputP_500MHz, byteBufferSize); 
            trigger.SetParameters(new EdgeTriggerParameters() { Level = 0, Hysteresis = 10, Direction = EdgeDirection.Falling });
            trigger.SetHorizontal(1000000, 0, 0);
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                trigger.Process(input: input, windowEndIndices: captureEndIndicesU64.Span, out uint captureEndCount);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            NativeMemory.Free(inputP_DC);
            NativeMemory.Free(inputP_500MHz);
            NativeMemory.Free(inputP_50percent);
        }
    }
}
