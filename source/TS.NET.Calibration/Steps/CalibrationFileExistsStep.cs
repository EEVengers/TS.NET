using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class CalibrationFileExistsStep : Step
{
    public CalibrationFileExistsStep(string name) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if (File.Exists(Variables.Instance.CalibrationFileName))
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration file exists: {Variables.Instance.CalibrationFileName}");
                return Status.Done;
            }
            else
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Error, $"Calibration file does not exist: {Variables.Instance.CalibrationFileName}");
                return Status.Error;
            }
        };
    }
}
