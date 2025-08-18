using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System.CommandLine;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using TS.NET;
using TS.NET.Engine;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // The aim is to have a thread-safe lock-free dataflow architecture (to prevent various classes of bugs).
        // The use of async/await for processing is avoided as the task thread pool is of little use here.
        //   Fire up threads to handle specific loops with extremely high utilisation. These threads are created once only, so the overhead of thread creation isn't important (one of the design goals of async/await).
        //   Optionally pin CPU cores to exclusively process a particular thread, perhaps with high/rt priority.
        //   Task.Factory.StartNew(() => Loop(...TaskCreationOptions.LongRunning) is just a shorthand for creating a new Thread to process a loop, the task thread pool isn't used.
        // The use of hardwareRequestChannel is to prevent 2 classes of bug: locking and thread safety.
        //   By serialising the config-update/data-read it also allows for specific behaviours (like pausing acquisition on certain config updates) and ensuring a perfect match between sample-block & hardware configuration that created it.

        Console.Title = "Engine";
        //using (Process p = Process.GetCurrentProcess())
        //    p.PriorityClass = ProcessPriorityClass.High;

        // To do: have something better than array index. Hardware serial?
        var deviceIndexOption = new Option<int>(name: "-i", description: "The ThunderScope to use if there are multiple connected to the host.", getDefaultValue: () => { return 0; });
        var configurationFilePathOption = new Option<string>(name: "-config", description: "Configuration file to use.", getDefaultValue: () => { return "thunderscope.yaml"; });
        var secondsOption = new Option<int>(name: "-seconds", description: "Run for an integer number of seconds. Useful for profiling.", getDefaultValue: () => { return 0; });
        var membenchOption = new Option<bool>(name: "-membench", description: "Run memory benchmark.", getDefaultValue: () => { return false; });

        var rootCommand = new RootCommand("TS.NET.Engine")
        {
            deviceIndexOption,
            configurationFilePathOption,
            secondsOption,
            membenchOption
        };

        rootCommand.SetHandler(Start, deviceIndexOption, configurationFilePathOption, secondsOption, membenchOption);
        return await rootCommand.InvokeAsync(args);
    }

    static void Start(int deviceIndex, string configurationFilePath, int seconds, bool membench)
    {
        ThunderscopeSettings? thunderscopeSettings = null;
        IThunderscope? thunderscope = null;

        void StopLibtslitex()
        {
            if (thunderscopeSettings != null)
            {
                switch (thunderscopeSettings.HardwareDriver.ToLower())
                {
                    case "litex":
                    case "libtslitex":
                        {
                            if (thunderscope != null)
                            {
                                try
                                {
                                    ((TS.NET.Driver.Libtslitex.Thunderscope)thunderscope).Close();
                                }
                                catch { }
                            }
                            break;
                        }
                }
            }
        }

        Console.CancelKeyPress += (sender, e) => { StopLibtslitex(); Environment.Exit(0); };    // Handle Ctrl+C or Ctrl+Break event.
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => { StopLibtslitex(); };            // Handle UI window close
        // Abrupt termination/SIGKILL to be handled with driver watchdog later?

        if (membench)
        {
            Utility.MemoryBenchmark();
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            return;
        }

        // In future; change this to lock per-device instead of a single global lock.
        //var lockFileName = $"TS.NET.lock";
        //var lockFilePath = Path.Combine(Path.GetTempPath(), lockFileName);

        try
        {
            // Commented out for now, more testing needed on Windows
            //using FileStream fs = new(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

#if DEBUG
            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(
                    YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance
                )
                .Build();
            string yaml = serializer.Serialize(ThunderscopeSettings.Default());
            File.WriteAllText("thunderscope (defaults).yaml", yaml);
#endif

            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddJsonFile(
                "appsettings.json"
            );
            var configuration = configurationBuilder.Build();
            thunderscopeSettings = ThunderscopeSettings.FromYamlFile(configurationFilePath);

            var loggerFactory = LoggerFactory.Create(configure =>
            {
                configure
                    .ClearProviders()
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddFile(configuration.GetSection("Logging"))
                    .AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "HH:mm:ss ";
                    });
            });
            var logger = loggerFactory.CreateLogger("TS.NET.Engine");

            if (RuntimeInformation.ProcessArchitecture == Architecture.X86 || RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                if (!Avx2.IsSupported)
                {
                    logger?.LogWarning("x86/x64 CPU without AVX2. CPU load will be high.");
                }
                else
                {
                    logger?.LogInformation("x86/x64 CPU with AVX2.");
                }
            }
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                if (!AdvSimd.Arm64.IsSupported)
                {
                    logger?.LogWarning("AArch64 CPU without Neon. CPU load will be high.");
                }
                else
                {
                    logger?.LogInformation("AArch64 CPU with Neon.");
                }
            }

            // Instantiate dataflow channels
            // XDMA bufferLength 60 (~0.5 seconds worth of samples at 1GSPS, 60x8388608 = 503316480)
            // LiteX bufferLength 3 (driver has large buffer)
            int bufferLength = 60;

            switch (thunderscopeSettings.HardwareDriver.ToLower())
            {
                case "simulation":
                    {
                        var ts = new TS.NET.Driver.Simulation.Thunderscope();
                        thunderscope = ts;
                        bufferLength = 3;
                        break;
                    }
                case "xdma":
                    {
                        // Find thunderscope
                        var devices = TS.NET.Driver.XMDA.Thunderscope.IterateDevices();
                        if (devices.Count == 0)
                        {
                            logger?.LogCritical("No thunderscopes found");
                            return;
                        }
                        if (deviceIndex > devices.Count - 1)
                        {
                            logger?.LogCritical($"Invalid thunderscope index ({deviceIndex}). Only {devices.Count} Thunderscopes connected.");
                            return;
                        }
                        var ts = new TS.NET.Driver.XMDA.Thunderscope(loggerFactory);
                        ThunderscopeHardwareConfig initialHardwareConfiguration = new();
                        initialHardwareConfiguration.AdcChannelMode = AdcChannelMode.Quad;
                        initialHardwareConfiguration.EnabledChannels = 0x0F;
                        initialHardwareConfiguration.SampleRateHz = 250000000;
                        initialHardwareConfiguration.Frontend[0] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[1] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[2] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[3] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Calibration[0] = thunderscopeSettings.XdmaCalibration.Channel1.ToDriver();
                        initialHardwareConfiguration.Calibration[1] = thunderscopeSettings.XdmaCalibration.Channel2.ToDriver();
                        initialHardwareConfiguration.Calibration[2] = thunderscopeSettings.XdmaCalibration.Channel3.ToDriver();
                        initialHardwareConfiguration.Calibration[3] = thunderscopeSettings.XdmaCalibration.Channel4.ToDriver();
                        ts.Open(devices[deviceIndex], initialHardwareConfiguration, thunderscopeSettings.HardwareRevision);
                        thunderscope = ts;
                        bufferLength = 60;
                        break;
                    }
                case "litex":
                case "libtslitex":
                    {
                        var ts = new TS.NET.Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);
                        ThunderscopeHardwareConfig initialHardwareConfiguration = new();
                        //initialHardwareConfiguration.AdcChannelMode = AdcChannelMode.Quad;
                        //initialHardwareConfiguration.EnabledChannels = 0x0F;
                        //initialHardwareConfiguration.SampleRateHz = 250000000;
                        initialHardwareConfiguration.AdcChannelMode = AdcChannelMode.Single;
                        initialHardwareConfiguration.EnabledChannels = 0x01;
                        initialHardwareConfiguration.SampleRateHz = 1000000000;
                        initialHardwareConfiguration.Frontend[0] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[1] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[2] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[3] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Calibration[0] = thunderscopeSettings.LiteXCalibration.Channel1.ToDriver();
                        initialHardwareConfiguration.Calibration[1] = thunderscopeSettings.LiteXCalibration.Channel2.ToDriver();
                        initialHardwareConfiguration.Calibration[2] = thunderscopeSettings.LiteXCalibration.Channel3.ToDriver();
                        initialHardwareConfiguration.Calibration[3] = thunderscopeSettings.LiteXCalibration.Channel4.ToDriver();
                        initialHardwareConfiguration.AdcCalibration = thunderscopeSettings.LiteXCalibration.Adc.ToDriver();
                        ts.Open((uint)deviceIndex, initialHardwareConfiguration);
                        thunderscope = ts;
                        bufferLength = 3;
                        break;
                    }
                default:
                    {
                        logger?.LogCritical($"{thunderscopeSettings.HardwareDriver} driver not supported");
                        return;
                    }
            }

            string bridgeNamespace = $"ThunderScope.{deviceIndex}";

            ThunderscopeMemoryRegion memoryRegion = new(bufferLength);
            BlockingChannel<ThunderscopeMemory> memoryChannel = new(bufferLength);
            for (uint i = 0; i < bufferLength; i++)
                memoryChannel.Writer.Write(memoryRegion.GetSegment(i));
            BlockingChannel<InputDataDto> incomingDataChannel = new();
            BlockingChannel<HardwareRequestDto> hardwareRequestChannel = new();
            BlockingChannel<HardwareResponseDto> hardwareResponseChannel = new();
            BlockingChannel<ProcessingRequestDto> processingRequestChannel = new();
            BlockingChannel<ProcessingResponseDto> processingResponseChannel = new();

            long captureBufferBytes = ((long)thunderscopeSettings.MaxCaptureLength) * 4 * ThunderscopeDataType.I16.ByteWidth();
            logger?.LogDebug($"{nameof(CaptureCircularBuffer)} bytes: {captureBufferBytes}");
            var captureBuffer = new CaptureCircularBuffer(captureBufferBytes);

            // Start threads
            SemaphoreSlim startSemaphore = new(1);

            startSemaphore.Wait();
            var processingThread = new ProcessingThread(
                loggerFactory: loggerFactory,
                settings: thunderscopeSettings,
                hardwareConfig: thunderscope.GetConfiguration(),
                incomingDataChannel: incomingDataChannel.Reader,
                memoryReturnChannel: memoryChannel.Writer,
                hardwareRequestChannel: hardwareRequestChannel.Writer,
                hardwareResponseChannel: hardwareResponseChannel.Reader,
                processingRequestChannel: processingRequestChannel.Reader,
                processingResponseChannel: processingResponseChannel.Writer,
                captureBuffer: captureBuffer);
            processingThread.Start(startSemaphore);

            startSemaphore.Wait();
            var hardwareThread = new HardwareThread(loggerFactory, thunderscopeSettings, thunderscope, memoryChannel.Reader, incomingDataChannel.Writer, hardwareRequestChannel.Reader, hardwareResponseChannel.Writer);
            hardwareThread.Start(startSemaphore);

            startSemaphore.Wait();
            var scpiServer = new ScpiServer(loggerFactory, thunderscopeSettings, System.Net.IPAddress.Any, 5025, hardwareRequestChannel.Writer, hardwareResponseChannel.Reader, processingRequestChannel.Writer, processingResponseChannel.Reader);
            scpiServer.Start(startSemaphore);

            startSemaphore.Wait();
            IEngineTask waveformBufferReader;
            switch (thunderscopeSettings.WaveformBufferReader)
            {
                case "DataServer":
                    DataServer dataServer = new(loggerFactory, thunderscopeSettings, System.Net.IPAddress.Any, 5026, captureBuffer);
                    waveformBufferReader = dataServer;
                    break;
                case "None":
                    waveformBufferReader = new EmptyWaveformBufferReader();
                    break;
                default:
                    logger?.LogCritical($"{thunderscopeSettings.WaveformBufferReader} waveform buffer reader not supported");
                    return;
            }
            waveformBufferReader.Start(startSemaphore);

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

            scpiServer.Stop();
            waveformBufferReader.Stop();
            hardwareThread.Stop();
            processingThread.Stop();
        }
        //catch (IOException)
        //{
        //    Console.WriteLine("Another instance of TS.NET.Engine is already running.");
        //    Thread.Sleep(3000);
        //    Environment.Exit(0);
        //}
        finally
        {
            StopLibtslitex();
        }
    }
}
