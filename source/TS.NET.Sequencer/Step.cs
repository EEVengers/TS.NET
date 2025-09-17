using System.Diagnostics;

namespace TS.NET.Sequencer;

public class Step
{
    public int Index { get; internal set; }
    public string Name { get; }
    public StepResult? Result { get; internal set; }
    public bool Skip { get; set; }
    public bool IgnoreError { get; set; }

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
        try
        {
            PreAction?.Invoke(cancellationTokenSource.Token);
            if (Skip)
                status = Status.Skipped;
            else if (Action != null)
                status = Action(cancellationTokenSource.Token);
            PostAction?.Invoke(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            status = Status.Cancelled;
        }
        catch (Exception ex)
        {
            status = Status.Error;
            exception = ex;
            Logger.Instance.Log(LogLevel.Error, $"Error in step: {Name}, exception: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
        }
        return new StepResult() { Status = status, Duration = stopwatch.Elapsed, Exception = exception };
    }

    public override string ToString() => Name;
}
