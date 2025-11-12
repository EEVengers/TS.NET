using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BodePlotStep : Step
{
    public BodePlotStep(string name, int channelIndex, PgaPreampGain preamp, int ladder, double amplitude, bool attenuator, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            //uint rate = 660_000_000;
            uint rate = 1_000_000_000;
            var resolution = AdcResolution.EightBit;

            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(resolution);
            Instruments.Instance.SetThunderscopeRate(rate);

            //Instruments.Instance.SetSdgBodeSetup(channelIndex, 100);
            //Instruments.Instance.SetSdgBodeSetup(channelIndex, 200);
            //Instruments.Instance.SetSdgBodeSetup(channelIndex, 400);

            Instruments.Instance.SetSdgChannel(channelIndex);
            Instruments.Instance.SetSdgSine(channelIndex);
            Instruments.Instance.SetSdgParameterFrequency(channelIndex, 10_000_000);
            Instruments.Instance.SetSdgParameterAmplitude(channelIndex, amplitude);
            Instruments.Instance.SetSdgParameterOffset(channelIndex, 0);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, preamp, ladder, variables);
            Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, attenuator: attenuator, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, ThunderscopeBandwidth.BwFull, variables);

            //var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);

            var frequenciesHz = new List<uint>();
            uint startFrequency = 1000;
            int decades = 4; // 1kHz -> 10kHz -> 100kHz -> 1MHz -> 10MHz
            int pointsPerDecade = 50;

            for (int d = 0; d < decades; d++)
            {
                for (int i = 0; i < pointsPerDecade; i++)
                {
                    uint frequency = (uint)(startFrequency * Math.Pow(10, d) * Math.Pow(10, (double)i / pointsPerDecade));
                    if (!frequenciesHz.Contains(frequency)) // Avoid duplicates that can occur due to rounding
                    {
                        frequenciesHz.Add(frequency);
                    }
                }
            }
            frequenciesHz.Add((uint)(startFrequency * Math.Pow(10, decades)));
      
            var bodePoints = new Dictionary<uint, double>();

            Instruments.Instance.SetSdgParameterFrequency(channelIndex, startFrequency);
            var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(channelIndex, startFrequency, rate, pathCalibration.BufferInputVpp, resolution);

            foreach (var frequencyHz in frequenciesHz)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Instruments.Instance.SetSdgParameterFrequency(channelIndex, frequencyHz);
                var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(channelIndex, frequencyHz, rate, pathCalibration.BufferInputVpp, resolution);

                // Normalise relative to the reference measurement
                double normalised = signalAtFrequency / signalAtRef;
                bodePoints[frequencyHz] = normalised;

                Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{channelIndex + 1}, frequency: {frequencyHz / 1e6:F3} MHz, scale: {normalised:F4}");
            }

            //var csv = bodePoints.Select(p => $"{p.Key},{p.Value:F4},{20.0 * Math.Log10(p.Value):F4}");
            //var csvString = "Frequency,Scale,dB\n" + string.Join("\n", csv);
            //File.WriteAllText($"{amplitude} Vpp - attenuator {attenuator}.csv", csvString);

            var metadata =
                new ResultMetadataXYChart()
                {
                    ShowInReport = true,
                    Title = $"Overall gain vs. frequency",
                    XAxis = new ResultMetadataXYChartAxis{ Label = "Frequency (Hz)", Scale = XYChartScaleType.Log10 },
                    YAxis = new ResultMetadataXYChartAxis{ Label = "Gain (dB)", Scale = XYChartScaleType.Linear, AdditionalRangeValues = [-0.3, 0.3] },
                    Series = new ResultMetadataXYChartSeries[]
                    {
                        new ResultMetadataXYChartSeries
                        {
                            Name = this.Name,
                            ColourHex = "#1f77b4",
                            Data = bodePoints.Select(p => new ResultMetadataXYChartDataPoint
                            {
                                X = (double)p.Key,
                                Y = 20.0 * Math.Log10(p.Value)
                            }).ToArray()
                        }
                    }
                };
            Result!.Metadata!.Add(metadata);

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Bode plot measurement complete for Ch{channelIndex + 1}.");
            return Status.Done;
        };
    }
}
