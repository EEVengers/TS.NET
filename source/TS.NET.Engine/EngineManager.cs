using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TS.NET.Engine;

// The aim is to have a thread-safe lock-free dataflow architecture (to prevent various classes of bugs).
// The use of async/await for processing is avoided as the task thread pool is of little use here.
//   Fire up threads to handle specific loops with extremely high utilisation. These threads are created once only, so the overhead of thread creation isn't important (one of the design goals of async/await).
//   Optionally pin CPU cores to exclusively process a particular thread, perhaps with high/rt priority.
//   Task.Factory.StartNew(() => Loop(...TaskCreationOptions.LongRunning) is just a shorthand for creating a new Thread to process a loop, the task thread pool isn't used.
// The use of BlockingRequestResponse is to prevent 2 classes of bug: locking and thread safety.
//   By serialising the config-update/data-read it also allows for specific behaviours (like pausing acquisition on certain config updates) and ensuring a perfect match between sample-block & hardware configuration that created it.
public class EngineManager
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;

    private ThunderscopeSettings? thunderscopeSettings = null;
    private IThunderscope? thunderscope = null;

    private ProcessingThread? processingThread;
    private ScpiServer? scpiServer;
    private IThread? waveformBufferReader;

    public BlockingChannel<INotificationDto>? UiNotifications;

    public EngineManager(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger(nameof(EngineManager));
    }

    public bool TryStart(string configurationFile, string calibrationFile, string deviceSerial)
    {
        Console.CancelKeyPress += (sender, e) => { StopLibtslitex(); Environment.Exit(0); };    // Handle Ctrl+C or Ctrl+Break event.
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => { StopLibtslitex(); };            // Handle UI window close

        // In future; change this to lock per-device instead of a single global lock.
        //var lockFileName = $"TS.NET.lock";
        //var lockFilePath = Path.Combine(Path.GetTempPath(), lockFileName);

        // Commented out for now, more testing needed on Windows
        //using FileStream fs = new(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        logger?.LogInformation($"Configuration path: {configurationFile}");

        thunderscopeSettings = ThunderscopeSettings.FromYamlFile(configurationFile);

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

        switch (thunderscopeSettings.HardwareDriver.ToLower())
        {
            case "simulation":
                {
                    var ts = new Driver.Simulation.Thunderscope();
                    thunderscope = ts;
                    break;
                }
            case "litex":
            case "libtslitex":
                {
                    string[] files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*tslitex*", SearchOption.TopDirectoryOnly);
                    if (files.Length == 0)
                    {
                        logger?.LogCritical($"tslitex not found");
                        return false;
                    }
                    var ts = new Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);

                    var devices = ts.ListDevices();
                    if (devices.Count > 0)
                    {
                        StringBuilder sb = new();
                        sb.AppendLine("");
                        sb.AppendLine("ThunderScopes:");
                        sb.AppendLine("");
                        foreach (var device in devices)
                        {
                            sb.AppendLine($"   DeviceID: {device.DeviceID}");
                            sb.AppendLine($"   DevicePath: {device.DevicePath.Trim()}");
                            sb.AppendLine($"   Identity: {device.Identity.Trim()}");
                            sb.AppendLine($"   SerialNumber: {device.SerialNumber.Trim()}");
                            sb.AppendLine($"   BuildConfig: {device.BuildConfig.Trim()}");
                            sb.AppendLine($"   BuildDate: {device.BuildDate.Trim()}");
                            sb.AppendLine($"   MfgSignature: {device.MfgSignature.Trim()}");
                            if (!device.Equals(devices.Last()))
                                sb.AppendLine("");

                        }
                        logger?.LogInformation(sb.ToString());
                    }

                    

                    // Later this will switch to using device serial.
                    uint deviceIndex = uint.Parse(deviceSerial);
                    ts.Open(deviceIndex);

                    ThunderscopeCalibrationSettings thunderscopeCalibrationSettings = new();
                    if (File.Exists(calibrationFile))
                    {
                        logger?.LogInformation($"Calibration loaded from path: {calibrationFile}");
                        thunderscopeCalibrationSettings = ThunderscopeCalibrationSettings.FromJsonFile(calibrationFile);
                    }
                    else if (ThunderscopeNonVolatileMemory.TryReadUserCalibration(ts, out var calibration))
                    {
                        logger?.LogInformation($"Calibration loaded from user calibration memory");
                        thunderscopeCalibrationSettings = calibration!;
                    }
                    else if (File.Exists("thunderscope-calibration.json"))
                    {
                        logger?.LogInformation($"Calibration loaded from thunderscope-calibration.json");
                        thunderscopeCalibrationSettings = ThunderscopeCalibrationSettings.FromJsonFile("thunderscope-calibration.json");
                    }
                    else
                    {
                        throw new ThunderscopeException("Could not load calibration from device or file");
                    }

                    ThunderscopeHardwareConfig initialHardwareConfiguration = new();
                    initialHardwareConfiguration.Acquisition = new ThunderscopeAcquisitionConfig
                    {
                        AdcChannelMode = AdcChannelMode.Single,
                        EnabledChannels = 0x01,
                        SampleRateHz = 1_000_000_000,
                        Resolution = AdcResolution.EightBit
                    };
                    initialHardwareConfiguration.Frontend[0] = ThunderscopeChannelFrontend.Default();
                    initialHardwareConfiguration.Frontend[1] = ThunderscopeChannelFrontend.Default();
                    initialHardwareConfiguration.Frontend[2] = ThunderscopeChannelFrontend.Default();
                    initialHardwareConfiguration.Frontend[3] = ThunderscopeChannelFrontend.Default();
                    initialHardwareConfiguration.Calibration[0] = thunderscopeCalibrationSettings.Channel1.ToDriver();
                    initialHardwareConfiguration.Calibration[1] = thunderscopeCalibrationSettings.Channel2.ToDriver();
                    initialHardwareConfiguration.Calibration[2] = thunderscopeCalibrationSettings.Channel3.ToDriver();
                    initialHardwareConfiguration.Calibration[3] = thunderscopeCalibrationSettings.Channel4.ToDriver();
                    initialHardwareConfiguration.AdcCalibration = thunderscopeCalibrationSettings.Adc.ToDriver();
                    initialHardwareConfiguration.ExternalSync = ThunderscopeExternalSync.Disabled;
                    ts.Configure(initialHardwareConfiguration, thunderscopeSettings.HardwareRevision);
                    thunderscope = ts;
                    break;
                }
            default:
                {
                    logger?.LogCritical($"{thunderscopeSettings.HardwareDriver} driver not supported");
                    return false;
                }
        }

        //string bridgeNamespace = $"ThunderScope.{deviceIndex}";
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl = new();

        long captureBufferBytes = ((long)thunderscopeSettings.MaxCaptureLength) * 4 * ThunderscopeDataType.I16.ByteWidth();
        logger?.LogDebug($"{nameof(CaptureCircularBuffer)} bytes: {captureBufferBytes}");
        var captureBuffer = new CaptureCircularBuffer(loggerFactory.CreateLogger(nameof(CaptureCircularBuffer)), captureBufferBytes);

        // Start threads
        SemaphoreSlim startSemaphore = new(1);

        startSemaphore.Wait();
        processingThread = new ProcessingThread(
            logger: loggerFactory.CreateLogger(nameof(ProcessingThread)),
            settings: thunderscopeSettings,
            thunderscope: thunderscope!,
            processingControl: processingControl,
            uiNotifications: UiNotifications?.Writer,
            captureBuffer: captureBuffer);
        processingThread.Start(startSemaphore);

        startSemaphore.Wait();
        scpiServer = new ScpiServer(
            logger: loggerFactory.CreateLogger(nameof(ScpiServer)),
            thunderscopeSettings,
            System.Net.IPAddress.Any,
            5025,
            processingControl);
        scpiServer.Start(startSemaphore);

        startSemaphore.Wait();
        switch (thunderscopeSettings.WaveformBufferReader)
        {
            case "DataServer":
                DataServer dataServer = new(loggerFactory.CreateLogger(nameof(DataServer)), thunderscopeSettings, System.Net.IPAddress.Any, 5026, captureBuffer, scpiServer);
                waveformBufferReader = dataServer;
                break;
            case "None":
                waveformBufferReader = new EmptyWaveformBufferReader();
                break;
            default:
                logger?.LogCritical($"{thunderscopeSettings.WaveformBufferReader} waveform buffer reader not supported");
                return false;
        }
        waveformBufferReader.Start(startSemaphore);

        //catch (IOException)
        //{
        //    Console.WriteLine("Another instance of TS.NET.Engine is already running.");
        //    Thread.Sleep(3000);
        //    Environment.Exit(0);
        //}
        return true;
    }

    public void Stop()
    {
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
