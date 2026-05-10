using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcLoadScaleStep : Step
{
    public AdcLoadScaleStep(string name, int[] channelIndices, uint sampleRateHz, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            if (!variables.TrimDacZeroCalibrated)
            {
                throw new TestbenchException($"Trim DAC zero must be calibrated before running {nameof(AdcLoadScaleStep)}");
            }
            if (!variables.BufferInputVppCalibrated)
            {
                throw new TestbenchException($"Buffer input Vpp must be calibrated before running {nameof(AdcLoadScaleStep)}");
            }
            if (channelIndices.Length == 0)
            {
                throw new TestbenchException("At least one channel index must be specified");
            }
            if (channelIndices.Length > 1 && sampleRateHz == 1_000_000_000)
            {
                throw new TestbenchException("Only one channel can have 1 GSPS");
            }

            var pgaGain = PgaPreampGain.Low;
            byte pgaLadder = 10;

            Instruments.Instance.SetThunderscopeChannel(channelIndices);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(sampleRateHz);

            List<double> scaleValues = new();

            // Special case - load scales are relative to the BufferInputVpp that is captured at 1 GSPS
            if (sampleRateHz == 1_000_000_000)
            {
                scaleValues.Add(1.0);
            }
            else
            {
                foreach (var channelIndex in channelIndices)
                {
                    var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
                    var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
                    var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
                    Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, variables.FrontEndSettlingTimeMs);

                    var pathConfig = Utility.GetChannelPathData(channelIndex, pgaGain, pgaLadder, variables);
                    var vpp = Utility.FindVpp(channelIndex, pathConfig, variables.SigGenZero, sampleRateHz, cancellationToken);
                    var scaleValue = Math.Round(vpp / pathCalibration.BufferInputVpp, 3);
                    scaleValues.Add(scaleValue);
                }
            }

            var loadScale = variables.Calibration.Adc.LoadScale.First(ls => ls.Channel.SequenceEqual(channelIndices));
            var rateLoadScale = loadScale.RateScale.First(x => x.Rate == sampleRateHz);
            rateLoadScale.Scale = scaleValues.ToArray();

            variables.ParametersSet += channelIndices.Length;

            var formattedScaleValues = string.Join(", ", scaleValues.Select(x => x.ToString("F3")));
            Result!.Summary = $"Scale: {formattedScaleValues}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Scale: {formattedScaleValues}");
            return Status.Done;
        };
    }
}
