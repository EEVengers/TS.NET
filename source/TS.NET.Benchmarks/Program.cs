using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using TS.NET.Benchmarks;

// https://github.com/dotnet/runtime/blob/main/src/coreclr/inc/clrconfigvalues.h#L718

var scalarConfig = ManualConfig.CreateMinimumViable().AddJob(Job.Default
        .WithEnvironmentVariable("DOTNET_EnableAVX", "0")
        .WithEnvironmentVariable("DOTNET_EnableSSSE3", "0")
        .WithEnvironmentVariable("DOTNET_EnableArm64AdvSimd", "0"))
    .WithOptions(ConfigOptions.JoinSummary)
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

var simdConfig = ManualConfig.CreateMinimumViable().AddJob(Job.Default)
    .WithOptions(ConfigOptions.JoinSummary)
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

var x64Ssse3Config = ManualConfig.CreateMinimumViable().AddJob(Job.Default
        .WithEnvironmentVariable("DOTNET_EnableAVX", "0")
        .WithEnvironmentVariable("DOTNET_EnableSSSE3", "1"))
    .WithOptions(ConfigOptions.JoinSummary)
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

BenchmarkRunner.Run([
    BenchmarkConverter.TypeToBenchmarks(typeof(ShuffleI8Benchmark), scalarConfig),
    //BenchmarkConverter.TypeToBenchmarks(typeof(ShuffleI8Benchmark), x64Ssse3Config),
    BenchmarkConverter.TypeToBenchmarks(typeof(ShuffleI8Benchmark), simdConfig),
    //BenchmarkConverter.TypeToBenchmarks(typeof(RisingEdgeTriggerI8Benchmark), scalarConfig) 
    ]);
//_ = BenchmarkRunner.Run(typeof(Program).Assembly);
//_ = BenchmarkRunner.Run<MemoryBenchmark>(config);

Console.ReadKey();