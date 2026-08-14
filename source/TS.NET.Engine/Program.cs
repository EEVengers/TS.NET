using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
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
        var settingsFilePathOption = new Option<string>(name: "-settings", description: "Settings file to use.", getDefaultValue: () => { return ""; });
        var calibrationFilePathOption = new Option<string>(name: "-calibration", description: "Calibration file to use.", getDefaultValue: () => { return ""; });
        var secondsOption = new Option<int>(name: "-seconds", description: "Run for an integer number of seconds. Useful for profiling.", getDefaultValue: () => { return 0; });
        var membenchOption = new Option<bool>(name: "-membench", description: "Run memory benchmark.", getDefaultValue: () => { return false; });
        var ngscopeclientOption = new Option<bool>(name: "-ngscopeclient", description: "Start ngscopeclient after the engine starts.", getDefaultValue: () => { return false; });
        var debugLogOption = new Option<bool>(name: "-debuglog", description: "Enable debug logging.", getDefaultValue: () => { return false; });

        var rootCommand = new RootCommand("TS.NET.Engine")
        {
            deviceIndexOption,
            settingsFilePathOption,
            calibrationFilePathOption,
            secondsOption,
            membenchOption,
            ngscopeclientOption,
            debugLogOption
        };

        rootCommand.SetHandler(Start, deviceIndexOption, settingsFilePathOption, calibrationFilePathOption, secondsOption, membenchOption, ngscopeclientOption, debugLogOption);
        return await rootCommand.InvokeAsync(args);
    }

    static void Start(int deviceIndex, string settingsFile, string calibrationFile, int seconds, bool membench, bool ngscopeclient, bool debugLog)
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

        var json = JsonSerializer.Serialize(Calibration.Default(), SourceGenerationContext.Default.Calibration) ?? throw new ArgumentNullException();
        File.WriteAllText("thunderscope-calibration (defaults).json", json);
#endif

        //IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddJsonFile("thunderscope-appsettings.json");
        //var configuration = configurationBuilder.Build();

#if DEBUG
        debugLog = true;    // Override debug log value
#endif

        Serilog.Core.Logger? serilog;
        if (debugLog)
        {
            serilog = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:w4}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/TS.NET.Engine.txt", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:w4}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
        else
        {
            serilog = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:w4}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        var loggerFactory = new LoggerFactory().AddSerilog(serilog);
        var logger = loggerFactory.CreateLogger("TS.NET.Engine");
        logger.LogInformation("Version: {Version}", typeof(Program).Assembly.GetName().Version?.ToString(3));
        var appCancellationTokenSource = new CancellationTokenSource();

        var engine = new EngineManager(loggerFactory, appCancellationTokenSource);
        var deviceSerial = deviceIndex.ToString();
        var persistWindow = true;
        if (engine.TryStart(settingsFile, calibrationFile, deviceSerial))
        {
            if (ngscopeclient && OperatingSystem.IsWindows())
            {
                var ngscopeclientPaths = new[]
                {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ThunderScope", "ngscopeclient", "ngscopeclient.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ngscopeclient", "ngscopeclient.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ngscopeclient", "ngscopeclient.exe")
            };
                var ngscopeclientPath = ngscopeclientPaths.FirstOrDefault(File.Exists);

                if (ngscopeclientPath is not null)
                {
                    var ngscopeclientProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = ngscopeclientPath,
                        Arguments = "ThunderScope:thunderscope:twinlan:127.0.0.1:5025:5026",
                        WorkingDirectory = Path.GetDirectoryName(ngscopeclientPath)!,
                        UseShellExecute = true
                    });
                    logger.LogInformation($"ngscopeclient started.");

                    if (ngscopeclientProcess is not null)
                    {
                        ngscopeclientProcess.Exited += (_, _) =>
                        {
                            logger.LogInformation("ngscopeclient exited, stopping engine.");
                            appCancellationTokenSource.Cancel();
                            persistWindow = false;
                        };
                        ngscopeclientProcess.EnableRaisingEvents = true;
                    }
                }
                else
                {
                    logger.LogWarning("ngscopeclient not found.");
                }
            }

            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            while (!appCancellationTokenSource.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.Escape:
                            persistWindow = false;
                            appCancellationTokenSource.Cancel();
                            break;
                    }
                }
                else
                {
                    if (seconds > 0)
                    {
                        if (DateTimeOffset.UtcNow.Subtract(startTime).TotalSeconds >= seconds)
                        {
                            persistWindow = false;
                            appCancellationTokenSource.Cancel();
                        }
                    }
                    Thread.Sleep(100);
                }
            }

            engine.Stop();
        }

        if (persistWindow)
        {
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
