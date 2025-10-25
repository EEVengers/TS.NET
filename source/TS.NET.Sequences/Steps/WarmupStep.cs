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
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }
}
