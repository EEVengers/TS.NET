using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BodePlotStep : Step
{
    public BodePlotStep(string name, int channelIndex, int[] channelIndices, uint sampleRateHz, PgaPreampGain preamp, int ladder, bool attenuator, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var resolution = AdcResolution.EightBit;

            Instruments.Instance.SetThunderscopeChannel(channelIndices);
            Instruments.Instance.SetThunderscopeResolution(resolution);
            Instruments.Instance.SetThunderscopeRate(sampleRateHz);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, preamp, ladder, variables);
            double attenuatorScale = channelIndex switch {
                0 => variables.Calibration.Channel1.AttenuatorScale,
                1 => variables.Calibration.Channel2.AttenuatorScale,
                2 => variables.Calibration.Channel3.AttenuatorScale,
                3 => variables.Calibration.Channel4.AttenuatorScale,
                _ => throw new NotImplementedException()
            };
            double amplitudeVpp = pathCalibration.BufferInputVpp * 0.8 / (attenuator ? attenuatorScale : 1.0);
            
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgLoad(channelIndex, ThunderscopeTermination.FiftyOhm);
            SigGens.Instance.SetSdgSine(channelIndex);
            SigGens.Instance.SetSdgParameterFrequency(channelIndex, 1000);
            SigGens.Instance.SetSdgParameterAmplitude(channelIndex, amplitudeVpp);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);

            Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, attenuator: attenuator, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, ThunderscopeBandwidth.BwFull, variables.FrontEndSettlingTimeMs);

            Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{channelIndex + 1}, {amplitudeVpp:F4} Vpp");
            //var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);

            var frequenciesHz = new List<uint>();
            uint startFrequencyHz = 100;
            int decades = 6;
            int pointsPerDecade = 40;

            for (int d = 0; d < decades; d++)
            {
                for (int i = 0; i < pointsPerDecade; i++)
                {
                    uint frequency = (uint)(startFrequencyHz * Math.Pow(10, d) * Math.Pow(10, (double)i / pointsPerDecade));
                    if(frequency > 40_000_000)
                        continue; // Limit due to SDG output limits
                    if (!frequenciesHz.Contains(frequency)) // Avoid duplicates that can occur due to rounding
                    {
                        frequenciesHz.Add(frequency);
                    }
                }
            }
            //frequenciesHz.Add((uint)(startFrequencyHz * Math.Pow(10, decades)));
            frequenciesHz.Add(40_000_000);
      
            var bodePoints = new Dictionary<uint, double>();

            SigGens.Instance.SetSdgParameterFrequency(channelIndex, startFrequencyHz);
            var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(channelIndex, startFrequencyHz, sampleRateHz, pathCalibration.BufferInputVpp, resolution) / (attenuator ? attenuatorScale : 1.0);

            foreach (var frequencyHz in frequenciesHz)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SigGens.Instance.SetSdgParameterFrequency(channelIndex, frequencyHz);
                var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(channelIndex, frequencyHz, sampleRateHz, pathCalibration.BufferInputVpp, resolution) / (attenuator ? attenuatorScale : 1.0);

                // Normalise relative to the reference measurement
                double normalised = signalAtFrequency / signalAtRef;
                bodePoints[frequencyHz] = normalised;

                Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{channelIndex + 1}, frequency: {frequencyHz / 1e3:F3} kHz, scale: {normalised:F4}");
            }

            //var csv = bodePoints.Select(p => $"{p.Key},{p.Value:F4},{20.0 * Math.Log10(p.Value):F4}");
            //var csvString = "Frequency,Scale,dB\n" + string.Join("\n", csv);
            //File.WriteAllText($"{amplitude} Vpp - attenuator {attenuator}.csv", csvString);

            var tableMetadata = 
            new ResultMetadataTable(){
                ShowInReport = true,
                Name = null,
                Headers = ["Parameter", "Value"],
                Rows = [
                        // ["Preamp", preamp.ToString()],
                        // ["Ladder", $"L{ladder}"],
                        // ["Attenuator", attenuator ? "On" : "Off"],
                        // ["Sample rate", $"{sampleRateHz / 1_000_000:F0} MSPS"],
                        // ["Resolution", resolution.ToString()],
                        ["Amplitude", $"{amplitudeVpp:F4} Vpp"],
                        ["Points", bodePoints.Count.ToString()]]
            };

            var metadata =
                new ResultMetadataXYChart()
                {
                    ShowInReport = true,
                    Title = $"Overall gain vs. frequency",
                    XAxis = new ResultMetadataXYChartAxis{ Label = "Frequency (Hz)", Scale = XYChartScaleType.Log10 },
                    YAxis = new ResultMetadataXYChartAxis{ Label = "Gain (dB)", Scale = XYChartScaleType.Linear, AdditionalRangeValues = [-0.3, 0.3] },
                    Series =
                    [
                        new ResultMetadataXYChartSeries
                        {
                            Name = this.Name,
                            ColourHex = "#1f77b4",
                            Data = bodePoints.Select(p => new ResultMetadataXYChartPoint
                            {
                                X = (double)p.Key,
                                Y = 20.0 * Math.Log10(p.Value)
                            }).ToArray()
                        }
                    ]
                };
            Result!.Metadata!.Add(tableMetadata);
            Result!.Metadata!.Add(metadata);

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Bode plot measurement complete for Ch{channelIndex + 1}.");
            return Status.Done;
        };
    }
}
