using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace TS.NET.Benchmarks
{
    [MemoryDiagnoser]
    public unsafe class ShuffleI8Benchmark
    {
        private const int byteBufferSize = 1 << 23;
        private void* inputP;
        private void* outputP;

        [GlobalSetup]
        public void Setup()
        {
            inputP = NativeMemory.AlignedAlloc(byteBufferSize, 32);
            outputP = NativeMemory.AlignedAlloc(byteBufferSize, 32);
            var input = new Span<sbyte>((sbyte*)inputP, byteBufferSize); 
            Waveforms.FourChannelCountSignedByte(input);
        }

        [Benchmark(Description = "Four channel shuffle, run-length 1 (SIMD, optimised)")]
        public void FourChannelsSimdOptimised1G()
        {
            var input = new Span<sbyte>((sbyte*)inputP, byteBufferSize); 
            var output = new Span<sbyte>((sbyte*)outputP, byteBufferSize); 
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                ShuffleI8.FourChannels(input, output);
        }

        [Benchmark(Description = "Four channel shuffle, run-length 1 (no SIMD, optimised)")]
        public void FourChannelsNoSimdOptimised1G()
        {
            var input = new Span<sbyte>((sbyte*)inputP, byteBufferSize); 
            var output = new Span<sbyte>((sbyte*)outputP, byteBufferSize); 
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                ShuffleI8Benchmarks.FourChannelsNoSimd(input, output);
        }

        [Benchmark(Description = "Two channel shuffle, run-length 1 (SIMD, optimised)")]
        public void TwoChannelsSimdOptimised1G()
        {
            var input = new Span<sbyte>((sbyte*)inputP, byteBufferSize); 
            var output = new Span<sbyte>((sbyte*)outputP, byteBufferSize); 
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                ShuffleI8.TwoChannels(input, output);
        }

        [Benchmark(Description = "Two channel shuffle, run-length 1 (no SIMD, optimised)")]
        public void TwoChannelsNoSimdOptimised1G()
        {
            var input = new Span<sbyte>((sbyte*)inputP, byteBufferSize); 
            var output = new Span<sbyte>((sbyte*)outputP, byteBufferSize); 
            // Processing 120 blocks of 8MiB is an approximation of 1 second of production usage
            for (int i = 0; i < 120; i++)
                ShuffleI8Benchmarks.TwoChannelsNoSimd(input, output);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            NativeMemory.Free(inputP);
            NativeMemory.Free(outputP);
        }
    }
}
