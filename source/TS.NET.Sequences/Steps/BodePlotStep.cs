using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BodePlotStep : Step
{
    public required int ChannelIndex { get; set; }
    public required int[] ChannelsEnabled { get; set; }
    public required PgaPreampGain PgaPreampGain { get; set; }
    public required int PgaLadder { get; set; }
    public required bool Attenuator { get; set; }
    public required uint SampleRateHz { get; set; }
    public required uint MaxFrequency { get; set; }
    
    public BodePlotStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var resolution = AdcResolution.EightBit;

            Instruments.Instance.SetThunderscopeChannel(ChannelsEnabled);
            Instruments.Instance.SetThunderscopeResolution(resolution);
            Instruments.Instance.SetThunderscopeRate(SampleRateHz);

            var pathCalibration = Utility.GetChannelPathCalibration(ChannelIndex, PgaPreampGain, PgaLadder, variables);
            double attenuatorScale = ChannelIndex switch {
                0 => variables.Calibration.Channel1.AttenuatorScale,
                1 => variables.Calibration.Channel2.AttenuatorScale,
                2 => variables.Calibration.Channel3.AttenuatorScale,
                3 => variables.Calibration.Channel4.AttenuatorScale,
                _ => throw new NotImplementedException()
            };
            double amplitudeVpp = pathCalibration.BufferInputVpp * 0.8 / (Attenuator ? attenuatorScale : 1.0);
            
            SigGens.Instance.SetSdgChannel([ChannelIndex]);
            SigGens.Instance.SetSdgLoad(ChannelIndex, ThunderscopeTermination.FiftyOhm);
            SigGens.Instance.SetSdgSine(ChannelIndex);
            SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, 1000);
            SigGens.Instance.SetSdgParameterAmplitude(ChannelIndex, amplitudeVpp);
            SigGens.Instance.SetSdgParameterOffset(ChannelIndex, 0);

            Instruments.Instance.SetThunderscopeCalManual50R(ChannelIndex, Attenuator, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, ThunderscopeBandwidth.BwFull, variables.FrontEndSettlingTimeMs);

            Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{ChannelIndex + 1}, {amplitudeVpp:F4} Vpp");
            //var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);

            var frequenciesHz = new List<uint>();
            uint startFrequencyHz = 100;
            int pointsPerDecade = 40;

            bool continueLoop = true;
            for (int d = 0; d < 10 && continueLoop; d++)
            {
                for (int i = 0; i < pointsPerDecade && continueLoop; i++)
                {
                    uint frequency = (uint)(startFrequencyHz * Math.Pow(10, d) * Math.Pow(10, (double)i / pointsPerDecade));
                    if(frequency > MaxFrequency)
                    {
                        continueLoop = false;
                        break;
                    }
                    if (!frequenciesHz.Contains(frequency)) // Avoid duplicates that can occur due to rounding
                    {
                        frequenciesHz.Add(frequency);
                    }
                }
            }
            if (!frequenciesHz.Contains(MaxFrequency))
            {
                frequenciesHz.Add(MaxFrequency);
            }
            //frequenciesHz.Add((uint)(startFrequencyHz * Math.Pow(10, decades)));
      
            var bodePoints = new Dictionary<uint, double>();

            SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, startFrequencyHz);
            var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, startFrequencyHz, SampleRateHz, pathCalibration.BufferInputVpp, resolution) / (Attenuator ? attenuatorScale : 1.0);

            foreach (var frequencyHz in frequenciesHz)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, frequencyHz);
                var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, frequencyHz, SampleRateHz, pathCalibration.BufferInputVpp, resolution) / (Attenuator ? attenuatorScale : 1.0);

                // Normalise relative to the reference measurement
                double normalised = signalAtFrequency / signalAtRef;
                bodePoints[frequencyHz] = normalised;

                Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{ChannelIndex + 1}, frequency: {frequencyHz / 1e3:F3} kHz, scale: {normalised:F4}");
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
                        ["Termination", "50R"],
                        ["PGA preamp", PgaPreampGain.ToString()],
                        ["PGA ladder", ReportStringUtility.LadderIndexToDbToHumanReadable(PgaLadder)],
                        ["Attenuator", Attenuator ? "On" : "Off"],
                        ["Sample rate", ReportStringUtility.SampleRateHzToHumanReadable(SampleRateHz)],
                        ["Resolution", ReportStringUtility.AdcResolutionToHumanReadable(resolution)],
                        ["Amplitude", $"{amplitudeVpp:F4} Vpp"],
                        ["Points", bodePoints.Count.ToString()]]
            };

            var metadata =
                new ResultMetadataXYChart()
                {
                    ShowInReport = true,
                    Title = $"Overall gain vs. frequency",
                    XAxis = new ResultMetadataXYChartAxis{ Label = "Frequency (Hz)", Scale = XYChartScaleType.Log10 },
                    YAxis = new ResultMetadataXYChartAxis{ Label = "Gain (dB)", Scale = XYChartScaleType.Linear, AdditionalRangeValues = [-0.5, 0.5] },
                    Series =
                    [
                        new ResultMetadataXYChartSeries
                        {
                            Name = Name,
                            ColourHex = "",
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

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Bode plot measurement complete for Ch{ChannelIndex + 1}.");
            return Status.Done;
        };
    }
}
