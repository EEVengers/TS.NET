using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimDacScaleStep : Step
{
    public TrimDacScaleStep(string name, int channelIndex, PgaPreampGain pgaGain, byte pgaLadder, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            var pathData = Utility.GetChannelPathData(channelIndex, pgaGain, pgaLadder, variables);
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);

            if (!Utility.TryTrimDacBinarySearch(channelIndex, pathCalibration, 114, 126, variables.TrimSettlingTimeMs, cancellationToken, out ushort pos100Dac, out double pos100Average))
            {
                throw new TestbenchException("Could not converge");
            }
            if (!Utility.TryTrimDacBinarySearch(channelIndex, pathCalibration, -126, -114, variables.TrimSettlingTimeMs, cancellationToken, out ushort neg100Dac, out double neg100Average))
            {
                throw new TestbenchException("Could not converge");
            }

            var max8bAdcCountPerDacLsb = pathData.Target8bAdcCountPerDacLsb * 1.2;
            var adcCountPerDacCount = Math.Abs((pos100Average - neg100Average) / (pos100Dac - neg100Dac));
            var dacCountFullScale = 256.0 / adcCountPerDacCount;

            if (adcCountPerDacCount < max8bAdcCountPerDacLsb)
            {
                pathCalibration.TrimDacScale = Math.Round(dacCountFullScale, 1);
                variables.ParametersSet++;
                Result!.Summary = $"Scale: {pathCalibration.TrimDacScale:F1}";
                Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"8b ADC count per DAC count: {adcCountPerDacCount:F3}, ideal: {pathData.Target8bAdcCountPerDacLsb:F3}, max limit: {max8bAdcCountPerDacLsb:F3}");
                return Status.Passed;
            }
            else
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Failed, $"8b ADC count per DAC count outside limits: {adcCountPerDacCount:F3}, ideal: {pathData.Target8bAdcCountPerDacLsb:F3}, max limit: {max8bAdcCountPerDacLsb:F3}");
                return Status.Failed;
            }
        };
    }
}
