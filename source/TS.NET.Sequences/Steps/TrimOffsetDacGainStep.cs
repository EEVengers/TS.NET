using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimOffsetDacGainStep : Step
{
    public TrimOffsetDacGainStep(string name, int channelIndex, int pathIndex, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeRate(1_000_000_000, variables);
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pathIndex, variables);
            DebugLog.Instance.Log($"TrimOffsetDacGainStep, channelIndex: {channelIndex}, pathIndex: {pathIndex}");

            pathCalibration.TrimScaleDac = pathConfig.TrimScaleDacInitial;

            if (!TryDacBinarySearch(channelIndex, pathCalibration, 118, 120, variables, cancellationToken, out int pos100Dac, out double pos100Average))
            {
                throw new TestbenchException("Could not converge");
            }
            if (!TryDacBinarySearch(channelIndex, pathCalibration, -120, -118, variables, cancellationToken, out int neg100Dac, out double neg100Average))
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

    public bool TryDacBinarySearch(
        int channelIndex,
        ThunderscopeChannelPathCalibration pathCalibration,
        double targetMin, double targetMax,
        CalibrationVariables variables,
        CancellationToken cancellationToken,
        out int dac, out double adc
        )
    {
        int low = 0;
        int high = 4095;
        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int mid = low + (high - low) / 2;
            Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, (ushort)mid, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
            var average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            if (average >= targetMin && average <= targetMax)
            {
                dac = mid;
                adc = average;
                return true;
            }
            if (average > targetMax)
            {
                low = mid + 1;
            }
            else if (average < targetMin)
            {
                high = mid - 1;
            }
        }
        dac = 0;
        adc = 0;
        return false;
    }
}
