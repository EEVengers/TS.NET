using System.Diagnostics;
using System.Runtime.InteropServices;
using TimeZoneConverter;

namespace TS.NET.Sequencer;

public class Sequence
{
    public string? Name { get; set; }
    public Step[]? Steps { get; set; }
    private Status? status;
    public Status? Status { get { return status; } set { status = value; SequenceStatusChanged?.Invoke(value); } }
    public DateTimeOffset StartTimestamp { get; set; }
    public string? TzId { get; set; }
    public TimeSpan? Duration { get; set; }

    public Action<Step>? PreStep;
    public Action<Step>? PostStep;
    public Action<Status?>? SequenceStatusChanged;

    public void PreRun()
    {
        if (Steps == null)
            return;

        // Clear results
        foreach (var step in Steps)
        {
            step.Result = null;
        }
    }

    public async Task Run(CancellationTokenSource cancellationTokenSource)
    {
        if (Steps == null)
            return;

        Logger.Instance.Log(LogLevel.Information, $"Starting sequence with {Steps.Length} steps.");
        Status = Sequencer.Status.Running;
        Duration = null;
        TzId = GetCurrentIanaTimeZoneId();
        StartTimestamp = DateTimeOffset.Now;

        var stopwatch = Stopwatch.StartNew();
        bool overallTermination = false;
        for (int i = 0; i < Steps.Length; i++)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {   // If a step didn't see the CTS (not implemented or chance timing), set overallTermination
                overallTermination = true;
                break;
            }
            PreStep?.Invoke(Steps[i]);
            await Task.Run(() => Steps[i].Run(cancellationTokenSource));
            PostStep?.Invoke(Steps[i]);
            if (!Steps[i].IgnoreError && (Steps[i].Result.Status == Sequencer.Status.Failed || Steps[i].Result.Status == Sequencer.Status.Error || Steps[i].Result.Status == Sequencer.Status.Cancelled))
                break;
        }
        // Always run Cleanup step if it exists and it didn't run
        if (Steps.Any(s => s.Name == "Cleanup" && s.Result == null))
        {
            var cleanupStep = Steps.First(s => s.Name == "Cleanup");
            PreStep?.Invoke(cleanupStep);
            await Task.Run(() => cleanupStep.Run(cancellationTokenSource));
            PostStep?.Invoke(cleanupStep);
        }

        stopwatch.Stop();
        Duration = stopwatch.Elapsed;
        Logger.Instance.Log(LogLevel.Information, $"Sequence complete.");

        // Check for overall state in priority order
        var sequenceStatus = Sequencer.Status.Passed;
        if (overallTermination || Steps.Any(s => s.Result?.Status == Sequencer.Status.Cancelled))
            sequenceStatus = Sequencer.Status.Cancelled;
        else if (Steps.Any(s => s.Result?.Status == Sequencer.Status.Failed))
            sequenceStatus = Sequencer.Status.Failed;
        else if (Steps.Any(s => s.Result?.Status == Sequencer.Status.Error))
            sequenceStatus = Sequencer.Status.Error;
        else if (Steps.All(s => s.Result?.Status == Sequencer.Status.Skipped))
            sequenceStatus = Sequencer.Status.Skipped;
        else if (Steps.All(s => s.Result?.Status == Sequencer.Status.Done))
            sequenceStatus = Sequencer.Status.Done;

        Status = sequenceStatus;
    }

    public void SetStepIndices()
    {
        if (Steps == null)
            return;
        int i = 1;
        foreach (var step in Steps)
        {
            step.Index = i;
            i++;
        }
    }

    public static string GetCurrentIanaTimeZoneId()
    {
        string localId = TimeZoneInfo.Local.Id;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Convert Windows ID → IANA ID
            return TZConvert.WindowsToIana(localId);
        }

        // On Linux/macOS, the ID is already IANA
        return localId;
    }
}
