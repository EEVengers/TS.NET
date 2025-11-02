using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class WarmupStep : Step
{
    public WarmupStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Logger.Instance.Log(LogLevel.Information, Index, $"Warming up for {variables.WarmupTimeSec} seconds");
            Task.Delay(variables.WarmupTimeSec * 1000, cancellationToken).Wait(cancellationToken);
            Result!.Summary = $"Time: {HumanDuration(TimeSpan.FromSeconds(variables.WarmupTimeSec))}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }

    private static string HumanDuration(TimeSpan duration)
    {
        if (duration.TotalHours > 1)
            return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
        else if (duration.TotalMinutes > 1)
            return $"{duration.Minutes}m {duration.Seconds}s";
        else
            return $"{duration.Seconds}.{duration.Milliseconds:D3}s";
    }
}
