using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class InitialiseDeviceStep : Step
{
    public InitialiseDeviceStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.Initialise();
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Device initialised");
            return Status.Done;
        };
    }
}
