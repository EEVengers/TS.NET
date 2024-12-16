using System.Diagnostics;

namespace TS.NET.Engine
{
    public static class Utility
    {
        record BenchmarkScenario(int Iterations, int CopySize);

        public static void MemoryBenchmark()
        {
            List<BenchmarkScenario> benchmarks =
            [
                new BenchmarkScenario(8000, 1 << 21),
                new BenchmarkScenario(4000, 1 << 22),
                new BenchmarkScenario(2000, 1 << 23),
                new BenchmarkScenario(1000, 1 << 24),
            ];

            foreach (var benchmark in benchmarks)
            {

                Span<byte> source = new byte[benchmark.CopySize];
                Span<byte> destination = new byte[benchmark.CopySize];

                var position = Console.GetCursorPosition();
                Console.WriteLine("Running warmup... 0%");
                for (int i = 0; i < benchmark.Iterations; i++)
                {
                    source.CopyTo(destination);
                    Console.SetCursorPosition(position.Left, position.Top);
                    Console.WriteLine($"Running warmup... {(i / (double)benchmark.Iterations) * 100:F0}%");
                }
                Console.SetCursorPosition(position.Left, position.Top);
                Console.WriteLine("Running warmup... 100%");

                position = Console.GetCursorPosition();
                Console.WriteLine("Running benchmark... 0%");
                List<double> results = new();
                for (int i = 0; i < benchmark.Iterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    source.CopyTo(destination);
                    stopwatch.Stop();

                    results.Add(benchmark.CopySize / stopwatch.Elapsed.TotalSeconds);

                    Console.SetCursorPosition(position.Left, position.Top);
                    Console.WriteLine($"Running benchmark... {(i / (double)benchmark.Iterations) * 100:F0}%");
                }
                Console.SetCursorPosition(position.Left, position.Top);
                Console.WriteLine($"Running benchmark... 100%");

                var average = results.Average();
                var stdDev = results.StandardDeviation();
                Console.WriteLine($"Single threaded memory copy, {benchmark.CopySize} byte block, {benchmark.Iterations} runs.");
                Console.WriteLine($"Mean: {average / 1e6:F3} MB/s, StdDev: {stdDev / 1e6:F3} MB/s");
            }
        }

        public static double StandardDeviation(this IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }
    }
}
