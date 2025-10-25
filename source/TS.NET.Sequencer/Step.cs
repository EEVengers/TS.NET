using System.Diagnostics;

namespace TS.NET.Sequencer;

public class Step
{
    public int Index { get; internal set; }
    public string Name { get; }
    public StepResult? Result { get; internal set; }
    public bool Skip { get; set; }
    public bool IgnoreError { get; set; }
    public TimeSpan? Timeout { get; set; }
    public int? Retries { get; set; }

    public Action<CancellationToken>? PreAction { get; set; }
    public Func<CancellationToken, Status>? Action { get; set; }
    public Action<CancellationToken>? PostAction { get; set; }

    // UI
    public bool AllowSkip { get; set; }

    public Step(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public StepResult Run(CancellationTokenSource cancellationTokenSource)
    {
        var stopwatch = Stopwatch.StartNew();
        Status status = Status.Running;
        Exception? exception = null;
        int retryCount = 0;
        bool timeout = false;
    RunStep:
        try
        {
            timeout = false;
            PreAction?.Invoke(cancellationTokenSource.Token);
            if (Skip)
                status = Status.Skipped;
            else if (Action != null)
            {
                var token = cancellationTokenSource.Token;
                CancellationTokenSource? timeoutCts = null;
                try
                {
                    if (Timeout != null)
                    {
                        timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        timeoutCts.CancelAfter(Timeout.Value);
                        token = timeoutCts.Token;
                    }
                    status = Action(token);
                }
                finally
                {
                    timeoutCts?.Dispose();
                }
            }
            PostAction?.Invoke(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            status = Status.Cancelled;
            // Check if it was a timeout rather than a regular cancellation
            if (Timeout != null && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                timeout = true;
                // If timeout occurred and there are going to be retries, log a timeout message. Otherwise an error is logged later on.
                if (retryCount < Retries)
                {
                    Logger.Instance.Log(LogLevel.Information, Index, "Timeout occurred");
                }
            }
        }
        catch (Exception ex)
        {
            status = Status.Error;
            exception = ex;
            Logger.Instance.Log(LogLevel.Error, $"Error in step: {Name}, {ex.GetType().Name}: {ex.Message}");
        }
        // If retries have been set up, handle that for Failed/Error and timeout
        if (Retries != null && (status == Status.Failed || status == Status.Error || timeout) && retryCount < Retries)
        {
            retryCount++;
            Logger.Instance.Log(LogLevel.Information, Index, $"Retrying step (attempt {retryCount}/{Retries})");
            goto RunStep;
        }
        else if (timeout)
        {
            // If timeout, and all the retries (if any) failed, change the status to error
            status = Status.Error;
            Logger.Instance.Log(LogLevel.Information, Index, Status.Error, $"Timeout occurred");
        }
        stopwatch.Stop();
        return new StepResult() { Status = status, Duration = stopwatch.Elapsed, Exception = exception };
    }

    public override string ToString() => Name;
}
