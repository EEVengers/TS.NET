using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class SaveUserCalToDeviceStep : Step
{
    public SaveUserCalToDeviceStep(string name) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.WriteUserCalibration(Variables.Instance.Calibration);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration saved to device");
            return Status.Done;
        };
    }
}
