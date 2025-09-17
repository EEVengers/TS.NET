using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class CalibrationFileLoadStep : Step
{
    public CalibrationFileLoadStep(string name) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Variables.Instance.Calibration = ThunderscopeCalibrationSettings.FromJsonFile(Variables.Instance.CalibrationFileName);
            Variables.Instance.ParametersSet = 0;
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration file loaded: {Variables.Instance.CalibrationFileName}");
            return Status.Done;
        };
    }
}
