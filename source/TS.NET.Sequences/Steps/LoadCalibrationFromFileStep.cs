using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class LoadCalibrationFromFileStep : Step
{
    public LoadCalibrationFromFileStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if (File.Exists(variables.CalibrationFileName))
            {
                variables.Calibration = Calibration.FromJsonFile(variables.CalibrationFileName);
                variables.ParametersSet = 0;
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from file");
                return Status.Done;
            }

            Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Calibration file not found");
            return Status.Error;
        };
    }
}
