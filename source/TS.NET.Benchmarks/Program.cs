using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using TS.NET.Benchmarks;

var config = ManualConfig.CreateMinimumViable().AddJob(Job.Default);
//var config = ManualConfig.CreateMinimumViable().AddJob(Job.Default.WithEnvironmentVariable("COMPlus_EnableAVX", "0"));
//DefaultConfig.Instance.WithOptions(ConfigOptions.JoinSummary);
//_ = BenchmarkRunner.Run(typeof(Program).Assembly);
_ = BenchmarkRunner.Run<ShuffleI8Benchmark>(config);
//_ = BenchmarkRunner.Run<RisingEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<FallingEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<AnyEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<PipelineBenchmark>();
//_ = BenchmarkRunner.Run<BoxcarAverageI8Benchmark>();
//_ = BenchmarkRunner.Run<SumU8toI32Benchmark>();
//_ = BenchmarkRunner.Run<DecimationI8Benchmark>();
Console.ReadKey();