using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class InitialiseDeviceStep : Step
{
    public InitialiseDeviceStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.InitialiseThunderscope();
            Instruments.Instance.SetThunderscopeChannel([0]);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000, variables);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Device initialised");
            return Status.Done;
        };
    }
}
