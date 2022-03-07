using Cloudtoid.Interprocess;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TS.NET;

Console.Title = "Simulator";
using (Process p = Process.GetCurrentProcess())
    p.PriorityClass = ProcessPriorityClass.High;

int samplingRate = 1000000000;
int byteBufferSize = 8000000;
int frequency = 1000000;
int samplesForOneCycle = samplingRate / frequency;

// Configure interprocess comms
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Publisher");
var factory = new QueueFactory(loggerFactory);
var options = new QueueOptions(queueName: "ThunderScope", bytesCapacity: 4 * byteBufferSize);
using var publisher = factory.CreatePublisher(options);

Memory<byte> sineBytes = new byte[byteBufferSize];
Waveforms.FourChannelSine(sineBytes.Span, samplingRate, frequency);

// Transmit messages
int counter = 0;
int previousCount = 0;
var startTimestamp = DateTime.UtcNow;
int totalTime = 0;
Stopwatch oneSecond = Stopwatch.StartNew();
while (true)
{
    if (publisher.TryEnqueue(sineBytes.Span))
    {
        counter++;
        //logger.LogInformation($"Enqueue #{counter}");
    }
    totalTime += 8;

    if (oneSecond.ElapsedMilliseconds >= 1000)
    {
        Console.WriteLine($"Counter: {counter}, counts/sec: {counter - previousCount} samples sent: {counter * 8000000}");
        previousCount = counter;
        oneSecond.Restart();
    }

    var duration = DateTime.UtcNow - startTimestamp;
    var sleepTime = totalTime - (int)duration.TotalMilliseconds;
    if (sleepTime < 0)
        sleepTime = 0;
    Thread.Sleep(sleepTime);
}