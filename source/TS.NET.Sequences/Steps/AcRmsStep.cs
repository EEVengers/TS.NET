using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AcRmsStep : Step
{
    public double MinLimit { get; set; } = double.MinValue;
    public double MaxLimit { get; set; } = double.MaxValue;

    public AcRmsStep(string name, int channelIndex, int pathIndex, ThunderscopeTermination termination, uint rateHz, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            switch (termination)
            {
                case ThunderscopeTermination.FiftyOhm:
                    Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
                    break;
                case ThunderscopeTermination.OneMegaohm:
                    Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
                    break;
            }
            Instruments.Instance.SetThunderscopeRate(rateHz, variables);

            var stdDev = Instruments.Instance.GetThunderscopePopulationStdDev(channelIndex);
            var stdDevV = stdDev * (pathCalibration.BufferInputVpp / 255.0);

            if (stdDevV >= MinLimit && stdDevV <= MaxLimit)
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"uVrms: {stdDevV * 1e6:F4}");
                return Status.Passed;
            }
            else
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Failed, $"uVrms: {stdDevV * 1e6:F4}");
                return Status.Failed;
            }
            
        };
    }
}
