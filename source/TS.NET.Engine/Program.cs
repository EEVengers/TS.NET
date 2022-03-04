using Microsoft.Extensions.Logging;
using TS.NET;
using TS.NET.Engine;

Console.Title = "Engine";

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
BlockingChannel<TriggeredCapture> triggeredCaptureChannel = new();

// Tasks started in 'reverse' order...

// Forward any triggered captures to a UI
TriggeredCaptureForwarderTask triggeredCaptureForwarderTask = new();
triggeredCaptureForwarderTask.Start(loggerFactory, triggeredCaptureChannel.Reader);

// Receive data from Simulator and process
TriggeredCaptureInputTask triggeredCaptureInputTask = new();
triggeredCaptureInputTask.Start(loggerFactory, triggeredCaptureChannel.Writer);

Console.WriteLine("Running... press any key to stop");
Console.ReadKey();

triggeredCaptureInputTask.Stop();
triggeredCaptureForwarderTask.Stop();