using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class LoadCalibrationFromDefaultStep : Step
{
    public LoadCalibrationFromDefaultStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            variables.Calibration = Calibration.Default();
            variables.ParametersSet = 0;
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from default");
            return Status.Done;
        };
    }
}
