using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace TS.NET.Engine
{
    // The aim is to have a thread-safe lock-free dataflow architecture (to prevent various classes of bugs).
    // The use of async/await for processing is avoided as the task thread pool is of little use here.
    //   Fire up threads to handle specific loops with extremely high utilisation. These threads are created once only, so the overhead of thread creation isn't important (one of the design goals of async/await).
    //   Optionally pin CPU cores to exclusively process a particular thread, perhaps with high/rt priority.
    //   Task.Factory.StartNew(() => Loop(...TaskCreationOptions.LongRunning) is just a shorthand for creating a new Thread to process a loop, the task thread pool isn't used.
    // The use of BlockingRequestResponse is to prevent 2 classes of bug: locking and thread safety.
    //   By serialising the config-update/data-read it also allows for specific behaviours (like pausing acquisition on certain config updates) and ensuring a perfect match between sample-block & hardware configuration that created it.
    public class EngineManager
    {
        private ThunderscopeSettings? thunderscopeSettings = null;
        private IThunderscope? thunderscope = null;

        private HardwareThread? hardwareThread;
        private PreProcessingThread? preProcessingThread;
        private ProcessingThread? processingThread;
        private ScpiServer? scpiServer;
        private IEngineTask? waveformBufferReader;

        public void Start(string configurationFile, string calibrationFile, string deviceSerial)
        {
            Console.CancelKeyPress += (sender, e) => { StopLibtslitex(); Environment.Exit(0); };    // Handle Ctrl+C or Ctrl+Break event.
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => { StopLibtslitex(); };            // Handle UI window close

            // In future; change this to lock per-device instead of a single global lock.
            //var lockFileName = $"TS.NET.lock";
            //var lockFilePath = Path.Combine(Path.GetTempPath(), lockFileName);

            // Commented out for now, more testing needed on Windows
            //using FileStream fs = new(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddJsonFile(
                "thunderscope-appsettings.json"
            );
            var configuration = configurationBuilder.Build();
            thunderscopeSettings = ThunderscopeSettings.FromYamlFile(configurationFile);
            var thunderscopeCalibrationSettings = ThunderscopeCalibrationSettings.FromJsonFile(calibrationFile);

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
                case "litex":
                case "libtslitex":
                    {
                        // Check for tslitex.dll
                        if (!File.Exists("tslitex.dll"))
                        {
                            logger?.LogCritical($"tslitex DLL not found");
                            return;
                        }
                        var ts = new TS.NET.Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);


                        //ts.UserDataRead(ts)

                        ThunderscopeHardwareConfig initialHardwareConfiguration = new();
                        //initialHardwareConfiguration.AdcChannelMode = AdcChannelMode.Quad;
                        //initialHardwareConfiguration.EnabledChannels = 0x0F;
                        //initialHardwareConfiguration.SampleRateHz = 250000000;
                        initialHardwareConfiguration.AdcChannelMode = AdcChannelMode.Single;
                        initialHardwareConfiguration.EnabledChannels = 0x01;
                        initialHardwareConfiguration.SampleRateHz = 1000000000;
                        initialHardwareConfiguration.Resolution = AdcResolution.EightBit;
                        initialHardwareConfiguration.Frontend[0] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[1] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[2] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Frontend[3] = ThunderscopeChannelFrontend.Default();
                        initialHardwareConfiguration.Calibration[0] = thunderscopeCalibrationSettings.Channel1.ToDriver();
                        initialHardwareConfiguration.Calibration[1] = thunderscopeCalibrationSettings.Channel2.ToDriver();
                        initialHardwareConfiguration.Calibration[2] = thunderscopeCalibrationSettings.Channel3.ToDriver();
                        initialHardwareConfiguration.Calibration[3] = thunderscopeCalibrationSettings.Channel4.ToDriver();
                        initialHardwareConfiguration.AdcCalibration = thunderscopeCalibrationSettings.Adc.ToDriver();
                        // Later this will switch to using device serial.
                        uint deviceIndex = uint.Parse(deviceSerial);
                        ts.Open(deviceIndex, initialHardwareConfiguration);
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

            //string bridgeNamespace = $"ThunderScope.{deviceIndex}";

            BlockingPool<DataDto> hardwarePool = new(bufferLength);
            BlockingPool<DataDto> preProcessingPool = new(bufferLength);
            ThunderscopeMemoryRegion memoryRegion = new(bufferLength * 2);
            for (int i = 0; i < bufferLength / 2; i++)
            {
                var dataDto = new DataDto() { Memory = memoryRegion.GetSegment(i) };
                hardwarePool.Return.Writer.Write(dataDto);
            }

            for (int i = bufferLength / 2; i < bufferLength; i++)
            {
                var dataDto = new DataDto() { Memory = memoryRegion.GetSegment(i) };
                preProcessingPool.Return.Writer.Write(dataDto);
            }

            BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl = new();
            BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl = new();

            long captureBufferBytes = ((long)thunderscopeSettings.MaxCaptureLength) * 4 * ThunderscopeDataType.I16.ByteWidth();
            logger?.LogDebug($"{nameof(CaptureCircularBuffer)} bytes: {captureBufferBytes}");
            var captureBuffer = new CaptureCircularBuffer(captureBufferBytes);

            // Start threads
            SemaphoreSlim startSemaphore = new(1);

            startSemaphore.Wait();
            processingThread = new ProcessingThread(
                loggerFactory: loggerFactory,
                settings: thunderscopeSettings,
                hardwareConfig: thunderscope.GetConfiguration(),
                preProcessingPool: preProcessingPool,
                hardwareControl: hardwareControl,
                processingControl: processingControl,
                captureBuffer: captureBuffer);
            processingThread.Start(startSemaphore);

            startSemaphore.Wait();
            preProcessingThread = new PreProcessingThread(
                loggerFactory: loggerFactory,
                settings: thunderscopeSettings,
                hardwarePool: hardwarePool,
                preProcessingPool: preProcessingPool);
            preProcessingThread.Start(startSemaphore);

            startSemaphore.Wait();
            hardwareThread = new HardwareThread(
                loggerFactory: loggerFactory,
                settings: thunderscopeSettings,
                thunderscope: thunderscope,
                hardwarePool: hardwarePool,
                hardwareControl: hardwareControl);
            hardwareThread.Start(startSemaphore);

            startSemaphore.Wait();
            scpiServer = new ScpiServer(
                loggerFactory,
                thunderscopeSettings,
                System.Net.IPAddress.Any,
                5025,
                hardwareControl,
                processingControl);
            scpiServer.Start(startSemaphore);

            startSemaphore.Wait();
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

            //catch (IOException)
            //{
            //    Console.WriteLine("Another instance of TS.NET.Engine is already running.");
            //    Thread.Sleep(3000);
            //    Environment.Exit(0);
            //}
        }

        public void Stop()
        {            
            hardwareThread?.Stop();
            preProcessingThread?.Stop();
            processingThread?.Stop();
            scpiServer?.Stop();
            waveformBufferReader?.Stop();

            StopLibtslitex();
        }

        private void StopLibtslitex()
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
    }
}
