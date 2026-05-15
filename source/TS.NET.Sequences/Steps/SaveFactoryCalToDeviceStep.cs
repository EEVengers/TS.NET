using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class SaveFactoryCalToDeviceStep : Step
{
    public SaveFactoryCalToDeviceStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.AppendFactoryCalibration(variables.Calibration);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration saved to device");
            return Status.Done;
        };
    }
}
