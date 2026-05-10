using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimDacZeroHotStep : Step
{
    public TrimDacZeroHotStep(string name, int channelIndex, PgaPreampGain pgaGain, byte pgaLadder, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            var pathData = Utility.GetChannelPathData(channelIndex, pgaGain, pgaLadder, variables);
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);

            var high = pathData.Target8bAdcCountPerDacLsb * 0.8;
            var low = pathData.Target8bAdcCountPerDacLsb * -0.8;

            ushort dac = variables.LastDacValue;
            byte dpot = pathCalibration.TrimDPot;

            bool solutionFound = false;
            while (dac > (2047-1000) && dac < (2047+1000))
            {
                cancellationToken.ThrowIfCancellationRequested();

                Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, dac, dpot, pgaGain, pgaLadder, variables.TrimSettlingTimeMs);
                var average = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 1_000_000);

                if (average > high)
                    dac++;
                else if (average < low)
                    dac--;
                else
                {
                    var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
                    Result!.Summary = $"{temperature:F1}°C - DAC: {dac}";
                    Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"DAC: {dac}, 8b ADC average: {average:F2}, temperature: {temperature:F1}");
                    pathData.TrimDacZeroHotDac = dac;
                    pathData.TrimDacZeroHotTemp = temperature;
                    variables.LastDacValue = dac;

                    // Now calculate the calibration value
                    var m = (pathData.TrimDacZeroColdDac - pathData.TrimDacZeroHotDac) / (pathData.TrimDacZeroColdTemp - pathData.TrimDacZeroHotTemp);
                    var c = pathData.TrimDacZeroHotDac - (m * pathData.TrimDacZeroHotTemp);
                    pathCalibration.TrimDacZeroM = Math.Round(m, 2);
                    pathCalibration.TrimDacZeroC = Math.Round(c, 1);
                    Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"TrimDacZeroM: {pathCalibration.TrimDacZeroM}, TrimDacZeroC: {pathCalibration.TrimDacZeroC}°C");
                    variables.ParametersSet += 2;
                    return Status.Passed;
                }
            }
            if (!solutionFound)
            {
                throw new TestbenchException("Could not converge");
            }
            return Status.Passed;
        };
    }
}
