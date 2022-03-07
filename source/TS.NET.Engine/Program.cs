using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TS.NET;
using TS.NET.Engine;

Console.Title = "Engine";
using (Process p = Process.GetCurrentProcess())
    p.PriorityClass = ProcessPriorityClass.High;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
//BlockingChannel<TriggeredCapture> triggeredCaptureChannel = new();

// Tasks started in 'reverse' order...

// Forward any triggered captures to a UI
//TriggeredCaptureForwarderTask triggeredCaptureForwarderTask = new();
//triggeredCaptureForwarderTask.Start(loggerFactory, triggeredCaptureChannel.Reader);

// Receive data from Simulator and process
TriggeredCaptureInputTask triggeredCaptureInputTask = new();
triggeredCaptureInputTask.Start(loggerFactory);

Console.WriteLine("Running... press any key to stop");
Console.ReadKey();

triggeredCaptureInputTask.Stop();
//triggeredCaptureForwarderTask.Stop();