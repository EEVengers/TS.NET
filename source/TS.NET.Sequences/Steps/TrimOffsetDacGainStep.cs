using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimOffsetDacGainStep : Step
{
    public TrimOffsetDacGainStep(string name, int channelIndex, PgaPreampGain pgaGain, int pgaLadder, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pgaGain, pgaLadder, variables);

            pathCalibration.TrimScaleDac = pathConfig.TrimScaleDacInitial;

            if (!Utility.TryTrimDacBinarySearch(channelIndex, pathCalibration, 118, 120, variables.FrontEndSettlingTimeMs, cancellationToken, out ushort pos100Dac, out double pos100Average))
            {
                throw new TestbenchException("Could not converge");
            }
            if (!Utility.TryTrimDacBinarySearch(channelIndex, pathCalibration, -120, -118, variables.FrontEndSettlingTimeMs, cancellationToken, out ushort neg100Dac, out double neg100Average))
            {
                throw new TestbenchException("Could not converge");
            }

            var adcCountPerDacCount = Math.Abs((pos100Average - neg100Average) / (pos100Dac - neg100Dac));
            // Can also get approximate zero to narrow down search space in later step
            var zero = pos100Dac + ((neg100Dac - pos100Dac) / 2);

            //pathConfig.AdcCountPerDacCount = adcCountPerDacCount;
            if (adcCountPerDacCount < (pathConfig.TargetDPotResolution * 1.2))
            {
                //pathCalibration.TrimOffsetDacScaleV = Math.Round(pathConfig.AdcCountPerDacCount * (pathCalibration.BufferInputVpp / 256.0), 6);
                pathCalibration.TrimOffsetDacScale = Math.Round(adcCountPerDacCount / 256.0, 6);
                pathCalibration.TrimOffsetDacZero = (ushort)zero;       // Approximate zero, to speed up TrimOffsetDacZeroStep
                variables.ParametersSet++;
                Result!.Summary = $"Scale: {pathCalibration.TrimOffsetDacScale}";
                Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"ADC count per DAC count: {adcCountPerDacCount:F3} | Ideal: {pathConfig.TargetDPotResolution:F3} | Max limit: {pathConfig.TargetDPotResolution * 1.2:F3}");
                return Status.Passed;
            }
            else
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Failed, $"ADC count per DAC count outside limits: {adcCountPerDacCount:F3}| Ideal: {pathConfig.TargetDPotResolution:F3} | Max limit: {(pathConfig.TargetDPotResolution * 1.2):F3}");
                return Status.Failed;
            }
        };
    }
}
