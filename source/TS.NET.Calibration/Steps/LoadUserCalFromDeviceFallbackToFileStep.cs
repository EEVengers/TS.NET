using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class LoadUserCalFromDeviceFallbackToFileStep : Step
{
    public LoadUserCalFromDeviceFallbackToFileStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if(Instruments.Instance.TryReadUserCalibration(out var calibration))
            {
                variables.Calibration = calibration!;
                variables.ParametersSet = 0;
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from device");
                return Status.Done;
            }
            if (File.Exists(variables.CalibrationFileName))
            {
                variables.Calibration = ThunderscopeCalibrationSettings.FromJsonFile(variables.CalibrationFileName);
                variables.ParametersSet = 0;
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from file");
                return Status.Done;
            }          

            Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Calibration not found in device or file");
            return Status.Error;
        };
    }
}
