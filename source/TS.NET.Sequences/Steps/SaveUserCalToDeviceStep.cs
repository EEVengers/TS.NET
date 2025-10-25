using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class SaveUserCalToDeviceStep : Step
{
    public SaveUserCalToDeviceStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.WriteUserCalibration(variables.Calibration);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration saved to device");
            return Status.Done;
        };
    }
}
