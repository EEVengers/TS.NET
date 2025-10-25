using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class WarmupStep : Step
{
    public WarmupStep(string name, int seconds) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Task.Delay(seconds * 1000, cancellationToken).Wait(cancellationToken);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }
}
