using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class InitialiseDeviceStep : Step
{
    public InitialiseDeviceStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            variables.HwidSerial = Instruments.Instance.Initialise();
            Result!.Summary = $"{variables.HwidSerial}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Device initialised, serial: {variables.HwidSerial}");
            return Status.Done;
        };
    }
}
