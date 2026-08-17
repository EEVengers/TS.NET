using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace TS.NET.Engine;

// The use of async/await for processing is avoided as the task thread pool is of little use here.
//   Fire up threads to handle specific loops with extremely high utilisation. These threads are created once only, so the overhead of thread creation isn't important (one of the design goals of async/await).
//   Optionally pin CPU cores to exclusively process a particular thread, perhaps with high/rt priority.
//   Task.Factory.StartNew(() => Loop(...TaskCreationOptions.LongRunning) is just a shorthand for creating a new Thread to process a loop, the task thread pool isn't used.
public class EngineManager
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly CancellationTokenSource appCancellationTokenSource;

    private static Mutex? deviceMutex;     // OS reclaims mutex on process exit
    private static FileStream? deviceLockFile;
    private static readonly Lock deviceLockSync = new();
    private ThunderscopeSettings? thunderscopeSettings = null;
    private IThunderscope? thunderscope = null;

    private ProcessingThread? processingThread;
    private ScpiServer? scpiServer;
    private IThread? waveformBufferReader;

    public BlockingChannel<INotificationDto>? UiNotifications;

    public EngineManager(ILoggerFactory loggerFactory, CancellationTokenSource appCancellationTokenSource)
    {
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger(nameof(EngineManager));
        this.appCancellationTokenSource = appCancellationTokenSource;
    }

    public bool TryStart(string settingsFile, string calibrationFile, string deviceSerial)
    {
        Console.CancelKeyPress += (sender, e) => { StopLibtslitex(); Environment.Exit(0); };    // Handle Ctrl+C or Ctrl+Break event.
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => { StopLibtslitex(); };            // Handle UI window close

        // In future; change this to lock per-device instead of a single global lock.
        //var lockFileName = $"TS.NET.lock";
        //var lockFilePath = Path.Combine(Path.GetTempPath(), lockFileName);

        // Commented out for now, more testing needed on Windows
        //using FileStream fs = new(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        thunderscopeSettings = LoadSettings(settingsFile);

        if (!TryParseServer(thunderscopeSettings.ScpiServer, out var scpiEndpoint))
        {
            logger?.LogCritical($"Invalid SCPI server address: {thunderscopeSettings.ScpiServer}");
            return false;
        }
        if (!TryParseServer(thunderscopeSettings.DataServer, out var dataEndpoint))
        {
            logger?.LogCritical($"Invalid data server address: {thunderscopeSettings.DataServer}");
            return false;
        }

        if (RuntimeInformation.ProcessArchitecture == Architecture.X86 || RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            if (!Avx2.IsSupported)
            {
                logger?.LogWarning("x86/x64 CPU without AVX2. CPU load will be high.");
            }
            else
            {
                logger?.LogDebug("x86/x64 CPU with AVX2");
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
                logger?.LogDebug("AArch64 CPU with Neon");
            }
        }

        string thunderscopeSerial = "NO_SERIAL";
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
                    IReadOnlyList<Driver.Libtslitex.ThunderscopeLiteXDevice> devices;
                    try
                    {
                        devices = Driver.Libtslitex.Thunderscope.ListDevices();
                    }
                    catch (DllNotFoundException)
                    {
                        logger?.LogCritical("Unable to load libtslitex. (DllNotFoundException)");
                        return false;
                    }
                    catch (BadImageFormatException)
                    {
                        logger?.LogCritical("Unable to load libtslitex. (BadImageFormatException)");
                        return false;
                    }

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
                            sb.AppendLine($"   Serial: {device.Serial.Trim()}");
                            sb.AppendLine($"   BuildConfiguration: {device.BuildConfiguration.Trim()}");
                            sb.AppendLine($"   BuildDate: {device.BuildDate.Trim()}");
                            sb.AppendLine($"   ManufacturingSignature: {device.ManufacturingSignature.Trim()}");
                            if (!device.Equals(devices.Last()))
                                sb.AppendLine("");

                        }
                        logger?.LogDebug(sb.ToString());
                    }

                    if (devices.Count == 0)
                    {
                        logger?.LogCritical("No ThunderScopes found");
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(deviceSerial))
                    {
                        // Pick the first thunderscope
                        deviceSerial = devices[0].Serial;
                    }
                    deviceSerial = deviceSerial.Trim();

                    if(!devices.Any(d => d.Serial.Trim() == deviceSerial))
                    {
                        logger?.LogCritical($"ThunderScope with serial {deviceSerial} not found");
                        return false;
                    }

                    uint deviceIndex = 0;
                    for (int i = 0; i < devices.Count; i++)
                    {
                        if (devices[i].Serial.Trim() == deviceSerial)
                        {
                            deviceIndex = (uint)i;
                            break;
                        }
                    }

                    if (!OperatingSystem.IsMacOS())        // Remove this when tested on macOS
                    {
                        if (!TryAcquireDeviceMutex(deviceSerial))
                        {
                            logger?.LogCritical($"Another instance of TS.NET.Engine is already running for {deviceSerial}");
                            return false;
                        }
                    }

                    logger?.LogInformation($"Using ThunderScope with serial: {deviceSerial}");

                    var ts = new Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);
                    ts.Open(deviceIndex);

                    // Order of priority for loading calibration:
                    // 1. Calibration file specified on CLI
                    // 2. User calibration stored in memory (UCAL)
                    // 2. Factory calibration stored in memory (FCAL)
                    // 3. Calibration file thunderscope-calibration.json in current directory
                    Calibration loadedCalibration = new();
                    if (File.Exists(calibrationFile))
                    {
                        logger?.LogInformation($"Calibration loaded from path: {calibrationFile}");
                        loadedCalibration = Calibration.FromJsonFile(calibrationFile);
                    }
                    else if (ThunderscopeNonVolatileMemory.TryReadUserCalibration(ts, out var userCalibration))
                    {
                        logger?.LogInformation($"Calibration loaded from user calibration memory");
                        loadedCalibration = userCalibration!;
                    }
                    else if (ThunderscopeNonVolatileMemory.TryReadFactoryCalibration(ts, out var factoryCalibration))
                    {
                        logger?.LogInformation($"Calibration loaded from factory calibration memory");
                        loadedCalibration = factoryCalibration!;
                    }
                    else if (File.Exists("thunderscope-calibration.json"))
                    {
                        logger?.LogInformation($"Calibration loaded from thunderscope-calibration.json");
                        loadedCalibration = Calibration.FromJsonFile("thunderscope-calibration.json");
                    }
                    else
                    {
                        logger?.LogCritical("Could not load calibration from device or file");
                        return false;
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
                    initialHardwareConfiguration.ExtSyncMode = ThunderscopeExtSyncMode.Disabled;
                    initialHardwareConfiguration.RefClockMode = ThunderscopeRefClockMode.Disabled;
                    initialHardwareConfiguration.RefClockFrequencyHz = 10_000_000;
                    ts.Configure(initialHardwareConfiguration, loadedCalibration, thunderscopeSettings.HardwareRevision);
                    ts.StartMonitoring();
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
        var captureBuffer = new CaptureBufferManager(loggerFactory.CreateLogger(nameof(CaptureBufferManager)), captureBufferBytes);

        // Start threads
        SemaphoreSlim startSemaphore = new(1);

        DataServer? dataServer = null;
        switch (thunderscopeSettings.WaveformBufferReader)
        {
            case "DataServer":
                dataServer = new DataServer(loggerFactory.CreateLogger(nameof(DataServer)), thunderscopeSettings, dataEndpoint!, captureBuffer, seq => scpiServer?.OnUpdateSequence(seq));
                waveformBufferReader = dataServer;
                break;
            case "None":
                waveformBufferReader = new EmptyWaveformBufferReader();
                break;
            default:
                logger?.LogCritical($"{thunderscopeSettings.WaveformBufferReader} waveform buffer reader not supported");
                return false;
        }

        startSemaphore.Wait();
        processingThread = new ProcessingThread(
            logger: loggerFactory.CreateLogger(nameof(ProcessingThread)),
            appCancellationTokenSource: appCancellationTokenSource,
            settings: thunderscopeSettings,
            thunderscope: thunderscope!,
            processingControl: processingControl,
            uiNotifications: UiNotifications?.Writer,
            captureBufferManager: captureBuffer);
        processingThread.Start(startSemaphore);

        startSemaphore.Wait();
        scpiServer = new ScpiServer(
            logger: loggerFactory.CreateLogger(nameof(ScpiServer)),
            thunderscopeSettings,
            thunderscopeSerial,
            scpiEndpoint!,
            processingControl);
        scpiServer.Start(startSemaphore);

        startSemaphore.Wait();
        waveformBufferReader.Start(startSemaphore);

        //catch (IOException)
        //{
        //    Console.WriteLine("Another instance of TS.NET.Engine is already running.");
        //    Thread.Sleep(3000);
        //    Environment.Exit(0);
        //}
        return true;
    }

    private static bool TryParseServer(string value, out IPEndPoint? endpoint)
    {
        return IPEndPoint.TryParse(value, out endpoint);
    }

    private ThunderscopeSettings LoadSettings(string settingsFile)
    {
        // Order of priority for settings file:
        // 1. Settings file specified on CLI
        // 2. Settings file loaded from LocalApplicationData
        // 3. Default settings file created in LocalApplicationData
        // 4. Default settings file created in working directory
        // 5. Default settings from memory

        try
        {
            if (!string.IsNullOrWhiteSpace(settingsFile) && File.Exists(settingsFile))
            {
                var settings = ThunderscopeSettings.FromYamlFile(settingsFile);
                logger.LogInformation("Settings loaded from CLI");
                return settings;
            }
        }
        catch { }

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localApplicationDataSettingsFile = Path.Combine(localAppData, "ThunderScope", "TS.NET.Engine", "settings.yaml");
            // Windows: %LocalAppData%\ThunderScope\TS.NET.Engine\settings.yaml
            // macOS: ~/Library/Application Support/ThunderScope/TS.NET.Engine/settings.yaml
            // Linux: $XDG_CONFIG_HOME/ThunderScope/TS.NET.Engine/settings.yaml or $HOME/.config/ThunderScope/TS.NET.Engine/settings.yaml
            Directory.CreateDirectory(Path.GetDirectoryName(localApplicationDataSettingsFile)!);
            if (!File.Exists(localApplicationDataSettingsFile))
                WriteDefaultSettings(localApplicationDataSettingsFile);

            var settings = ThunderscopeSettings.FromYamlFile(localApplicationDataSettingsFile);
            logger.LogInformation("Settings loaded from LocalApplicationData");
            return settings;
        }
        catch { }

        const string workingDirectorySettingsFile = "settings.yaml";
        try
        {
            if (!File.Exists(workingDirectorySettingsFile))
                WriteDefaultSettings(workingDirectorySettingsFile);

            var settings = ThunderscopeSettings.FromYamlFile(workingDirectorySettingsFile);
            logger.LogInformation("Settings loaded from working directory");
            return settings;
        }
        catch { }

        logger.LogInformation("Settings loaded from default");
        return ReadDefaultSettings();
    }

    private static void WriteDefaultSettings(string settingsFile)
    {
        using var resourceStream = OpenDefaultSettingsStream();
        using var outputStream = File.Create(settingsFile);
        resourceStream.CopyTo(outputStream);
    }

    private static ThunderscopeSettings ReadDefaultSettings()
    {
        using var resourceStream = OpenDefaultSettingsStream();
        using var reader = new StreamReader(resourceStream);
        return ThunderscopeSettings.FromYaml(reader.ReadToEnd());
    }

    private static Stream OpenDefaultSettingsStream()
    {
        return typeof(EngineManager).Assembly.GetManifestResourceStream("TS.NET.Engine.settings.yaml")
            ?? throw new InvalidOperationException("Embedded default settings was not found.");
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

    private static bool TryAcquireDeviceMutex(string deviceSerial)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            return TryAcquireUnixDeviceLock(deviceSerial);

        string name = $"TS.NET.Engine.{deviceSerial}";
        try
        {
            deviceMutex = new Mutex(true, name, out var createdNew);

            if (createdNew)
                return true;

            deviceMutex.Dispose();
            deviceMutex = null;
            return false;

        }
        catch (AbandonedMutexException)
        {
            // Despite the exception, mutex was acquired
            return true;
        }
    }

    private static bool TryAcquireUnixDeviceLock(string deviceSerial)
    {
        string path = Path.Combine(Path.GetTempPath(), $"TS.NET.Engine.{deviceSerial}.lock");
        lock (deviceLockSync)
        {
            if (deviceLockFile is not null)
                return false;

            try
            {
                var lockFile = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                deviceLockFile = lockFile;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to acquire lock file '{path}': {ex}");
                return false;
            }
        }
    }

    public static void ReleaseDeviceMutexIfExists()
    {
        FileStream? lockFile;
        lock (deviceLockSync)
        {
            lockFile = deviceLockFile;
            deviceLockFile = null;
        }

        if (lockFile is not null)
        {
            lockFile.Dispose();
            return;
        }

        Mutex? mutex = Interlocked.Exchange(ref deviceMutex, null);
        if (mutex is null)
            return;

        try
        {
            mutex.ReleaseMutex();
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
