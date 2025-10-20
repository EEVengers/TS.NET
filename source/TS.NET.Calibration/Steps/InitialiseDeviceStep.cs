using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class InitialiseDeviceStep : Step
{
    public InitialiseDeviceStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.InitialiseThunderscope(variables.CalibrationFileName);
            Instruments.Instance.SetThunderscopeChannel([0]);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000, variables);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Device initialised");
            return Status.Done;
        };
    }
}
