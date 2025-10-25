using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class SaveUserCalToFileStep : Step
{
    public SaveUserCalToFileStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            variables.CalibrationTimestamp = DateTimeOffset.Now;
            variables.Calibration.CalibrationTimestamp = variables.CalibrationTimestamp.ToString("yyyy-MM-ddTHH:mm:ss");
            variables.Calibration.ToJsonFile($"thunderscope-calibration {variables.CalibrationTimestamp:yyyy-MM-dd_HHmmss}.json");
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration saved to file");
            return Sequencer.Status.Done;
        };
    }
}
