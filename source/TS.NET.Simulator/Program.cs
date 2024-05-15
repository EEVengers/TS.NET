using Cloudtoid.Interprocess;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TS.NET;

Console.Title = "Simulator";
using (Process p = Process.GetCurrentProcess())
    p.PriorityClass = ProcessPriorityClass.High;

int samplingRate = 1000000000;
uint byteBufferSize = ThunderscopeMemory.Length;
int frequency = 976562;
int samplesForOneCycle = samplingRate / frequency;

// Configure interprocess comms
using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => { options.SingleLine = true; options.TimestampFormat = "HH:mm:ss "; }).AddFilter(level => level >= LogLevel.Debug));
var logger = loggerFactory.CreateLogger("Simulator");
var factory = new QueueFactory(loggerFactory);
var options = new QueueOptions(queueName: "ThunderScope.Simulator", bytesCapacity: 4 * byteBufferSize);
using var publisher = factory.CreatePublisher(options);

Memory<byte> waveformBytes = new byte[byteBufferSize];
Waveforms.FourChannelSine(waveformBytes.Span, samplingRate, frequency);
//Waveforms.FourChannelCountSignedByte(waveformBytes.Span);

// Transmit messages
ulong counter = 0;
ulong previousCount = 0;
var startTimestamp = DateTime.UtcNow;
double totalTime = 0;
Stopwatch oneSecond = Stopwatch.StartNew();
while (true)
{
    var waveform = waveformBytes.Span;
    var waveformLength = waveform.Length;
    if (publisher.TryEnqueue(waveform))
    {
        counter++;
        //logger.LogInformation($"Enqueue #{counter}");
    }
    totalTime += 8.388608;

    if (oneSecond.ElapsedMilliseconds >= 1000)
    {
        logger.LogDebug($"Counter: {counter}, counts/sec: {counter - previousCount}, samples sent: {counter * byteBufferSize}");
        previousCount = counter;
        oneSecond.Restart();
    }

    var duration = DateTime.UtcNow - startTimestamp;
    var sleepTime = totalTime - duration.TotalMilliseconds;
    if (sleepTime < 0)
        sleepTime = 0;
    Thread.Sleep((int)sleepTime);
}