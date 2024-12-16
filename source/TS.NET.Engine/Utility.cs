using System.Diagnostics;

namespace TS.NET.Engine
{
    public static class Utility
    {
        public static void MemoryBenchmark()
        {
            const int runs = 10000;
            Span<byte> source = new byte[ThunderscopeMemory.Length];
            Span<byte> destination = new byte[ThunderscopeMemory.Length];

            var position = Console.GetCursorPosition();
            Console.WriteLine("Running warmup... 0%");
            for (int i = 0; i < runs; i++)
            {
                source.CopyTo(destination);
                Console.SetCursorPosition(position.Left, position.Top);
                Console.WriteLine($"Running warmup... {(i / (double)runs) * 100:F0}%");
            }
            Console.SetCursorPosition(position.Left, position.Top);
            Console.WriteLine("Running warmup... 100%");

            position = Console.GetCursorPosition();
            Console.WriteLine("Running benchmark... 0%");
            List<double> results = new();
            for (int i = 0; i < runs; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                source.CopyTo(destination);
                stopwatch.Stop();

                results.Add(ThunderscopeMemory.Length / stopwatch.Elapsed.TotalSeconds);

                Console.SetCursorPosition(position.Left, position.Top);
                Console.WriteLine($"Running benchmark... {(i / (double)runs) * 100:F0}%");
            }
            Console.SetCursorPosition(position.Left, position.Top);
            Console.WriteLine($"Running benchmark... 100%");

            var average = results.Average();
            var stdDev = results.StandardDeviation();
            Console.WriteLine($"Single threaded memory copy, {ThunderscopeMemory.Length} byte block, {runs} runs.");
            Console.WriteLine($"Mean: {average/1e6:F3} MB/s, StdDev: {stdDev / 1e6:F3} MB/s");
        }

        public static double StandardDeviation(this IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }
    }
}
