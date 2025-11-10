using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BodePlotStep : Step
{
    public BodePlotStep(string name, int channelIndex, int configIndex, double amplitude, bool attenuator, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            //uint rate = 660_000_000;
            uint rate = 1_000_000_000;
            var resolution = AdcResolution.EightBit;

            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(resolution);
            Instruments.Instance.SetThunderscopeRate(rate);

            Instruments.Instance.SetSdgChannel(channelIndex);
            Instruments.Instance.SetSdgSine(channelIndex, amplitude, 10_000_000);
            Instruments.Instance.SetSdgOffset(channelIndex, 0);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, configIndex, variables);
            //var pathConfig = Utility.GetChannelPathConfig(channelIndex, configIndex, variables);
            Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, attenuator: attenuator, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, ThunderscopeBandwidth.BwFull, variables);

            //var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);

            var frequenciesHz = new List<uint>();
            // Generate frequencies from 1kHz to 10MHz with 100 points per decade
            //double startFrequency = 100;
            double startFrequency = 10000;
            int decades = 3; // 1kHz -> 10kHz -> 100kHz -> 1MHz -> 10MHz
            int pointsPerDecade = 100;

            for (int d = 0; d < decades; d++)
            {
                for (int i = 0; i < pointsPerDecade; i++)
                {
                    uint frequency = (uint)(startFrequency * Math.Pow(10, (double)i / pointsPerDecade));
                    if (!frequenciesHz.Contains(frequency)) // Avoid duplicates that can occur due to rounding
                    {
                        frequenciesHz.Add(frequency);
                    }
                }
                startFrequency *= 10;
            }
            //if (!frequenciesHz.Contains(10_000_000))
            //{
            //    frequenciesHz.Add(10_000_000);
            //}

            var bodePoints = new Dictionary<uint, double>();

            Instruments.Instance.SetSdgSine(channelIndex, amplitude, 10_000_000);
            //var signalAt1MHz = Instruments.Instance.GetThunderscopeVppAtFrequency(channelIndex, 1_000_000, rate, pathCalibration.BufferInputVpp, resolution);
            //var signalAt1MHz = Instruments.Instance.GetThunderscopePopulationStdDev(channelIndex);
            var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(channelIndex, 10_000_000, rate, pathCalibration.BufferInputVpp, resolution);

            List<string[]> rows = [];
            foreach (var frequencyHz in frequenciesHz)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Instruments.Instance.SetSdgFrequency(channelIndex, frequencyHz);
                Thread.Sleep(100);
                //var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequency(channelIndex, frequencyHz, rate, pathCalibration.BufferInputVpp, resolution);
                //var signalAtFrequency = Instruments.Instance.GetThunderscopePopulationStdDev(channelIndex);
                var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(channelIndex, frequencyHz, rate, pathCalibration.BufferInputVpp, resolution);

                // Normalise relative to the reference measurement
                double normalised = signalAtFrequency / signalAtRef;
                bodePoints[frequencyHz] = normalised;
                rows.Add([$"{frequencyHz}", $"{20.0 * Math.Log10(normalised):F3}"]);

                Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{channelIndex + 1}, Cfg {configIndex}: Freq={frequencyHz / 1e6:F3}MHz, Scale={normalised:F4}");
            }

            var csv = bodePoints.Select(p => $"{p.Key},{p.Value}");//,{20.0 * Math.Log10(p.Value)}");
            var csvString = "Frequency,Scale\n" + string.Join("\n", csv);
            File.WriteAllText($"config {configIndex} - {amplitude} Vpp - attenuator {attenuator}.csv", csvString);

            var metadata = new List<ResultMetadata>
            {
                new ResultMetadataTable()
                {
                    Name = "Gain vs. frequency, normalised to 1 MHz",
                    ShowInReport = true,
                    Headers = ["Frequency (Hz)", "Gain (dB)"],
                    Rows = rows.ToArray()
                }
            };
            if (Result != null)
                Result.Metadata = metadata.ToArray();

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Bode plot measurement complete for Ch{channelIndex + 1}, Cfg {configIndex}.");
            return Status.Done;
        };
    }
}
