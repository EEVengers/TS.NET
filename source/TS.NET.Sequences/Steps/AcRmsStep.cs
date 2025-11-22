using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AcRmsStep : Step
{
    public double MinLimit { get; set; } = double.MinValue;
    public double MaxLimit { get; set; } = double.MaxValue;
    public int Averages { get; set; } = 1;

    public AcRmsStep(string name, int channelIndex, int pathIndex, ThunderscopeTermination termination, ThunderscopeBandwidth bandwidth, uint rateHz, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            switch (termination)
            {
                case ThunderscopeTermination.FiftyOhm:
                    Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, false, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, bandwidth, variables.FrontEndSettlingTimeMs);
                    break;
                case ThunderscopeTermination.OneMegaohm:
                    Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, false, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, bandwidth, variables.FrontEndSettlingTimeMs);
                    break;
            }

            double stdDevV = 0;
            for (int i = 0; i < Averages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stdDev = Instruments.Instance.GetThunderscopePopulationStdDev(channelIndex, sampleCount: 10_000_000);
                stdDevV += stdDev * (pathCalibration.BufferInputVpp / 256.0);
            }
            stdDevV /= Averages;

            if (stdDevV >= MinLimit && stdDevV <= MaxLimit)
            {
                Result!.Summary = $"uVrms: {stdDevV * 1e6:F1}";
                Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"uVrms: {stdDevV * 1e6:F1}");
                return Status.Passed;
            }
            else
            {
                Result!.Summary = $"uVrms: {stdDevV * 1e6:F1}";
                Logger.Instance.Log(LogLevel.Information, Index, Status.Failed, $"uVrms: {stdDevV * 1e6:F1}");
                return Status.Failed;
            }
            
        };
    }
}
