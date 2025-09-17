using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class WarmupStep : Step
{
    public WarmupStep(string name) : base(name)
    {
        AllowSkip = true;
        Action = (CancellationToken cancellationToken) =>
        {
            Variables.Instance.Calibration = ThunderscopeCalibrationSettings.FromJsonFile(Variables.Instance.CalibrationFileName);
            Variables.Instance.ParametersSet = 0;
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration file loaded: {Variables.Instance.CalibrationFileName}");
            return Status.Done;
        };
    }
}
