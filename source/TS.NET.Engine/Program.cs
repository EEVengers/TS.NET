using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TS.NET;
using TS.NET.Engine;

// The aim is to have a thread-safe lock-free dataflow architecture (to prevent various classes of bugs).
// The use of async/await for processing is avoided as the task thread pool is of little use here.
//   Fire up threads to handle specific loops with extremely high utilisation. These threads are created once only, so the overhead of thread creation isn't important (one of the design goals of async/await).
//   Future work might pin CPU cores to exclusively process a particular thread, perhaps with high/rt priority.
//   Task.Factory.StartNew(() => Loop(...TaskCreationOptions.LongRunning) is just a shorthand for creating a new Thread to process a loop, the task thread pool isn't used. 
// The use of hardwareRequestChannel is to prevent 2 classes of bug: locking and thread safety.
//   By serialising the config-update/data-read it also allows for specific behaviours (like pausing acquisition on certain config updates) and ensuring a perfect match between sample-block & hardware configuration that created it.

Console.Title = "Engine";
using (Process p = Process.GetCurrentProcess())
    p.PriorityClass = ProcessPriorityClass.High;

#if DEBUG
ThunderscopeSettings settings = ThunderscopeSettings.Default();
string json = System.Text.Json.JsonSerializer.Serialize(settings, SourceGenerationContext.Default.ThunderscopeSettings);
File.WriteAllText("thunderscope (defaults).json", json);

var serializer = new YamlDotNet.Serialization.SerializerBuilder()
    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
    .Build();
string yaml = serializer.Serialize(settings);
File.WriteAllText("thunderscope (defaults).yaml", yaml);
#endif

IConfigurationBuilder configurationBuilder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json");
var configuration = configurationBuilder.Build();
var thunderscopeSettings = ThunderscopeSettings.FromYamlFile("thunderscope.yaml");

var loggerFactory = LoggerFactory.Create(configure =>
{
    configure
        .ClearProviders()
        .AddConfiguration(configuration.GetSection("Logging"))
        .AddFile(configuration.GetSection("Logging"))
        .AddSimpleConsole(options => { options.SingleLine = true; options.TimestampFormat = "HH:mm:ss "; });
});

// Instantiate dataflow channels
const int bufferLength = 120;       // 120 = about 1 seconds worth of samples at 1GSPS (each ThunderscopeMemory is 8388608 bytes), 120x = 1006632960

ThunderscopeMemoryRegion memoryRegion = new(bufferLength);
BlockingChannel<ThunderscopeMemory> inputChannel = new(bufferLength);
for (uint i = 0; i < bufferLength; i++)
    inputChannel.Writer.Write(memoryRegion.GetSegment(i));
BlockingChannel<InputDataDto> processChannel = new();
BlockingChannel<HardwareRequestDto> hardwareRequestChannel = new();
BlockingChannel<HardwareResponseDto> hardwareResponseChannel = new();
BlockingChannel<ProcessingRequestDto> processingRequestChannel = new();
BlockingChannel<ProcessingResponseDto> processingResponseChannel = new();

Thread.Sleep(1000);

IThunderscope thunderscope;
switch (thunderscopeSettings.Driver.ToLower())
{
    case "simulator":
        {
            var ts = new TS.NET.Driver.Simulator.Thunderscope();
            thunderscope = ts;
            break;
        }
    case "xdma":
        {
            // Find thunderscope
            var devices = TS.NET.Driver.XMDA.Thunderscope.IterateDevices();
            if (devices.Count == 0)
                throw new Exception("No thunderscopes found");
            var ts = new TS.NET.Driver.XMDA.Thunderscope();
            ts.Open(devices[0], thunderscopeSettings.Calibration.ToDriver());
            thunderscope = ts;
            break;
        }
    default:
        throw new ArgumentException($"{thunderscopeSettings.Driver} driver not supported");
}

// Start threads
ControlThread controlThread = new(loggerFactory, thunderscopeSettings, hardwareRequestChannel.Writer, processingRequestChannel.Writer);
controlThread.Start();

ProcessingThread processingThread = new(loggerFactory, thunderscopeSettings, processChannel.Reader, inputChannel.Writer, processingRequestChannel.Reader, processingResponseChannel.Writer);
processingThread.Start();

HardwareThread hardwareThread = new(loggerFactory, thunderscopeSettings, thunderscope, inputChannel.Reader, processChannel.Writer, hardwareRequestChannel.Reader, hardwareResponseChannel.Writer);
hardwareThread.Start();

WaveformServer waveformServer = new(loggerFactory, thunderscopeSettings, System.Net.IPAddress.Any, 5026);
waveformServer.Start();

ScpiServer scpiServer = new(loggerFactory, System.Net.IPAddress.Any, 5025, hardwareRequestChannel.Writer, hardwareResponseChannel.Reader, processingRequestChannel.Writer, processingResponseChannel.Reader);
scpiServer.Start();

bool loop = true;
while (loop)
{
    var key = Console.ReadKey();
    switch (key.Key)
    {
        case ConsoleKey.Escape:
            loop = false;
            break;
    }
}

scpiServer.Stop();
waveformServer.Stop();
hardwareThread.Stop();
processingThread.Stop();
controlThread.Stop();