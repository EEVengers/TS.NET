using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TemperatureCheckStep : Step
{
    public TemperatureCheckStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var temp = Instruments.Instance.GetThunderscopeFpgaTemp();
            Result!.Summary = $"{temp:F1}°C";

            if (temp > 40.0)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Temperature too high - {temp:F1}°C > 40°C");
                return Status.Error;
            }

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Temperature within limit {temp:F1}°C <= 40°C");
            return Status.Passed;
        };
    }
}
