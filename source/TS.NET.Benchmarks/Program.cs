using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using TS.NET.Benchmarks;

DefaultConfig.Instance.WithOptions(ConfigOptions.JoinSummary);
//_ = BenchmarkRunner.Run(typeof(Program).Assembly);
//_ = BenchmarkRunner.Run<ShuffleI8Benchmark>();
//_ = BenchmarkRunner.Run<RisingEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<FallingEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<AnyEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<PipelineBenchmark>();
_ = BenchmarkRunner.Run<BoxcarAverageI8Benchmark>();
//_ = BenchmarkRunner.Run<SumU8toI32Benchmark>();
_ = BenchmarkRunner.Run<DecimationI8Benchmark>();
Console.ReadKey();