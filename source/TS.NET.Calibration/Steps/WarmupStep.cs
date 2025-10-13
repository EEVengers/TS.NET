using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class WarmupStep : Step
{
    public WarmupStep(string name) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Task.Delay(20*60*1000, cancellationToken).Wait(cancellationToken);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }
}
