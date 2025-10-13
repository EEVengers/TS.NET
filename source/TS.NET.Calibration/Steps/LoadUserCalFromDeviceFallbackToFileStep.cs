using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class LoadUserCalFromDeviceFallbackToFileStep : Step
{
    public LoadUserCalFromDeviceFallbackToFileStep(string name) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if(Instruments.Instance.TryReadUserCalibration(out var calibration))
            {
                Variables.Instance.Calibration = calibration!;
                Variables.Instance.ParametersSet = 0;
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from device");
                return Status.Done;
            }
            if (File.Exists(Variables.Instance.CalibrationFileName))
            {
                Variables.Instance.Calibration = ThunderscopeCalibrationSettings.FromJsonFile(Variables.Instance.CalibrationFileName);
                Variables.Instance.ParametersSet = 0;
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from file");
                return Status.Done;
            }          

            Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Calibration not found in device or file");
            return Status.Error;
        };
    }
}
