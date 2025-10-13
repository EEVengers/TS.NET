using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class SaveUserCalToFileStep : Step
{
    public SaveUserCalToFileStep(string name) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Variables.Instance.CalibrationTimestamp = DateTimeOffset.Now;
            Variables.Instance.Calibration.CalibrationTimestamp = Variables.Instance.CalibrationTimestamp.ToString("yyyy-MM-ddTHH:mm:ss");
            Variables.Instance.Calibration.ToJsonFile($"thunderscope-calibration {Variables.Instance.CalibrationTimestamp:yyyy-MM-dd_HHmmss}.json");
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration saved to file");
            return Sequencer.Status.Done;
        };
    }
}
