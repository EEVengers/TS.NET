using System.Diagnostics;
using System.Xml.Serialization;

namespace TS.NET.Sequencer;

[Serializable]
public class Step
{
    public int Index { get; set; }
    public string Name { get; set; }
    public bool Skip { get; set; }
    public bool IgnoreError { get; set; }
    public TimeSpan? Timeout { get; set; }
    public int? MaxRetries { get; set; }
    public Result? Result { get; set; }     // Last property so the order in serialization is better

    [XmlIgnore] public Action<CancellationToken>? PreAction { get; set; }
    [XmlIgnore] public Func<CancellationToken, Status>? Action { get; set; }
    [XmlIgnore] public Action<CancellationToken>? PostAction { get; set; }

    // UI
    [XmlIgnore] public bool AllowSkip { get; set; }

    public Step() { Name = "-"; }   // For serialization

    public Step(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void Run(CancellationTokenSource cancellationTokenSource)
    {
        // Result is set in Sequence.Run so that the UI shows a Running step with PreStep?.Invoke
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
                if (retryCount < MaxRetries)
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
        if (MaxRetries != null && (status == Status.Failed || status == Status.Error || timeout) && retryCount < MaxRetries)
        {
            retryCount++;
            Logger.Instance.Log(LogLevel.Information, Index, $"Retrying step (attempt {retryCount}/{MaxRetries})");
            goto RunStep;
        }
        else if (timeout)
        {
            // If timeout, and all the retries (if any) failed, change the status to error
            status = Status.Error;
            Logger.Instance.Log(LogLevel.Information, Index, Status.Error, $"Timeout occurred");
        }
        stopwatch.Stop();
        if (Result != null)
        {
            Result.Status = status;
            Result.Duration = stopwatch.Elapsed;
            if (status == Status.Error && exception != null)
                Result.Exception = exception;
        }
    }

    public override string ToString() => Name;

    public bool ShouldSerializeTimeout()
    {
        return Timeout.HasValue;
    }

    public bool ShouldSerializeMaxRetries()
    {
        return MaxRetries.HasValue;
    }
}
