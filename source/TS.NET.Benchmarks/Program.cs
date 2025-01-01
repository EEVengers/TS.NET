using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using TS.NET.Benchmarks;

bool simd = false;
var config = ManualConfig.CreateMinimumViable().AddJob(Job.Default
        .WithEnvironmentVariable("DOTNET_EnableAVX", simd ? "1" : "0")
        .WithEnvironmentVariable("DOTNET_EnableArm64AdvSimd", simd ? "1" : "0"))
    .WithOptions(ConfigOptions.JoinSummary)
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

BenchmarkRunner.Run([
    BenchmarkConverter.TypeToBenchmarks(typeof(ShuffleI8Benchmark), config),
    BenchmarkConverter.TypeToBenchmarks(typeof(RisingEdgeTriggerI8Benchmark), config) 
    ]);
//_ = BenchmarkRunner.Run(typeof(Program).Assembly);
//_ = BenchmarkRunner.Run<MemoryBenchmark>(config);

Console.ReadKey();