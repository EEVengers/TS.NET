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
                new BenchmarkScenario(1 << 15, 1 << 13),
                new BenchmarkScenario(1 << 15, 1 << 14),
                new BenchmarkScenario(1 << 15, 1 << 15),
                new BenchmarkScenario(1 << 15, 1 << 16),
                new BenchmarkScenario(1 << 15, 1 << 17),
                new BenchmarkScenario(1 << 14, 1 << 18),
                new BenchmarkScenario(1 << 13, 1 << 19),
                new BenchmarkScenario(1 << 12, 1 << 20),
                new BenchmarkScenario(1 << 11, 1 << 21),
                new BenchmarkScenario(1 << 10, 1 << 22),
                new BenchmarkScenario(1 << 9, 1 << 23),
                new BenchmarkScenario(1 << 8, 1 << 24),
            ];

            Console.WriteLine($"Single threaded memory copy");
            foreach (var benchmark in benchmarks)
            {
                Span<byte> source = new byte[benchmark.CopySize];
                Span<byte> destination = new byte[benchmark.CopySize];

                for (int i = 0; i < benchmark.Iterations; i++)
                {
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                }

                List<double> results = new();
                for (int i = 0; i < benchmark.Iterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    source.CopyTo(destination);
                    stopwatch.Stop();

                    results.Add(benchmark.CopySize / stopwatch.Elapsed.TotalSeconds);

                    //Console.SetCursorPosition(position.Left, position.Top);
                    //Console.WriteLine($"Running benchmark... {(i / (double)benchmark.Iterations) * 100:F0}%");
                }
                //Console.SetCursorPosition(position.Left, position.Top);
                //Console.WriteLine($"Running benchmark... 100%");

                var average = results.Average() * 8.0;
                var stdDev = results.StandardDeviation() * 8.0;
                Console.WriteLine($"{benchmark.CopySize} bytes, {benchmark.Iterations} runs, mean: {average / 1e6:F3} MB/s, SD: {stdDev / 1e6:F3} MB/s");
            }
        }

        public static double StandardDeviation(this IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }
    }
}
