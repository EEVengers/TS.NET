using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimDacZeroColdStep : Step
{
    public TrimDacZeroColdStep(string name, int channelIndex, PgaPreampGain pgaGain, byte pgaLadder, CalibrationVariables variables) : base(name)
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
            while (dac > (2047 - 1000) && dac < (2047 + 1000))
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
                    Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"DAC: {dac}, 8b ADC average: {average:F2}, temperature: {temperature:F1}°C");
                    pathData.TrimDacZeroColdDac = dac;
                    pathData.TrimDacZeroColdTemp = temperature;
                    variables.LastDacValue = dac;
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
