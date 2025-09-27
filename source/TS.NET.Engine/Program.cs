using System.CommandLine;
using System.Text.Json;
using TS.NET;
using TS.NET.Engine;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.Title = "Engine";
        //using (Process p = Process.GetCurrentProcess())
        //    p.PriorityClass = ProcessPriorityClass.High;

        // To do: have something better than array index. Hardware serial?
        var deviceIndexOption = new Option<int>(name: "-i", description: "The ThunderScope to use if there are multiple connected to the host.", getDefaultValue: () => { return 0; });
        var configurationFilePathOption = new Option<string>(name: "-config", description: "Configuration file to use.", getDefaultValue: () => { return "thunderscope.yaml"; });
        var calibrationFilePathOption = new Option<string>(name: "-calibration", description: "Calibration file to use.", getDefaultValue: () => { return "thunderscope-calibration.json"; });
        var secondsOption = new Option<int>(name: "-seconds", description: "Run for an integer number of seconds. Useful for profiling.", getDefaultValue: () => { return 0; });
        var membenchOption = new Option<bool>(name: "-membench", description: "Run memory benchmark.", getDefaultValue: () => { return false; });

        var rootCommand = new RootCommand("TS.NET.Engine")
        {
            deviceIndexOption,
            configurationFilePathOption,
            calibrationFilePathOption,
            secondsOption,
            membenchOption
        };

        rootCommand.SetHandler(Start, deviceIndexOption, configurationFilePathOption, calibrationFilePathOption, secondsOption, membenchOption);
        return await rootCommand.InvokeAsync(args);
    }

    static void Start(int deviceIndex, string configurationFile, string calibrationFile, int seconds, bool membench)
    {
        if (membench)
        {
            Utility.MemoryBenchmark();
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            return;
        }

#if DEBUG
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(
                YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance
            )
            .Build();
        var yaml = serializer.Serialize(ThunderscopeSettings.Default()) ?? throw new ArgumentNullException();
        File.WriteAllText("thunderscope (defaults).yaml", yaml);

        var json = JsonSerializer.Serialize(ThunderscopeCalibrationSettings.Default(), SourceGenerationContext.Default.ThunderscopeCalibrationSettings) ?? throw new ArgumentNullException();
        File.WriteAllText("thunderscope-calibration (defaults).json", json);
#endif

        var engine = new EngineManager();
        var deviceSerial = deviceIndex.ToString();
        engine.Start(configurationFile, calibrationFile, deviceSerial);

        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        bool loop = true;
        while (loop)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey();
                switch (key.Key)
                {
                    case ConsoleKey.Escape:
                        loop = false;
                        break;
                }
            }
            else
            {
                if (seconds > 0)
                {
                    if (DateTimeOffset.UtcNow.Subtract(startTime).TotalSeconds >= seconds)
                        break;
                }
                Thread.Sleep(100);
            }
        }

        engine.Stop();
    }
}
