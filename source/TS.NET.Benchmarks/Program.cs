using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using TS.NET.Benchmarks;

var config = ManualConfig.CreateMinimumViable().AddJob(Job.Default.WithEnvironmentVariable("COMPlus_EnableAVX", "0"))
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

//DefaultConfig.Instance.WithOptions(ConfigOptions.JoinSummary);

//_ = BenchmarkRunner.Run(typeof(Program).Assembly);
//_ = BenchmarkRunner.Run<MemoryBenchmark>(config);
//_ = BenchmarkRunner.Run<ShuffleI8Benchmark>(config);
_ = BenchmarkRunner.Run<RisingEdgeTriggerI8Benchmark>(config);

//_ = BenchmarkRunner.Run<FallingEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<AnyEdgeTriggerBenchmark>();
//_ = BenchmarkRunner.Run<PipelineBenchmark>();
//_ = BenchmarkRunner.Run<BoxcarAverageI8Benchmark>();
//_ = BenchmarkRunner.Run<SumU8toI32Benchmark>();
//_ = BenchmarkRunner.Run<DecimationI8Benchmark>();
Console.ReadKey();