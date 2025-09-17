using System.Diagnostics;

namespace TS.NET.Sequencer;

public class Sequence
{
    public string? Name { get; set; }
    public Step[]? Steps { get; set; }
    private Status? status;
    public Status? Status { get { return status; } set { status = value; SequenceStatusChanged?.Invoke(value); } }
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

        var stopwatch = Stopwatch.StartNew();
        bool overallTermination = false;
        for (int i = 0; i < Steps.Length; i++)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {   // If a step didn't see the CTS (not implemented or chance timing), set overallTermination
                overallTermination = true;
                break;
            }
            Steps[i].Result = new StepResult() { Status = Sequencer.Status.Running, Duration = null, Exception = null };
            PreStep?.Invoke(Steps[i]);
            var stepResult = await Task.Run(() => Steps[i].Run(cancellationTokenSource))
                .ContinueWith(t => t.Result);
            Steps[i].Result = stepResult;
            PostStep?.Invoke(Steps[i]);
            if (!Steps[i].IgnoreError && (stepResult.Status == Sequencer.Status.Failed || stepResult.Status == Sequencer.Status.Error || stepResult.Status == Sequencer.Status.Cancelled))
                break;
        }
        // Always run Cleanup step if it exists and it didn't run
        if (Steps.Any(s => s.Name == "Cleanup" && s.Result == null))
        {
            var cleanupStep = Steps.First(s => s.Name == "Cleanup");
            cleanupStep.Result = new StepResult() { Status = Sequencer.Status.Running, Duration = null, Exception = null };
            PreStep?.Invoke(cleanupStep);
            var stepResult = await Task.Run(() => cleanupStep.Run(cancellationTokenSource))
                .ContinueWith(t => t.Result);
            cleanupStep.Result = stepResult;
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

    public void SetIndices()
    {
        if (Steps == null)
            return;
        int i = 0;
        foreach (var step in Steps)
        {
            step.Index = i;
            i++;
        }
    }
}
