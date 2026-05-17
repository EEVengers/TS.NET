using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BodePlotGpsdoStep : Step
{
    public required int ChannelIndex { get; set; }
    public required int[] ChannelsEnabled { get; set; }
    public required PgaPreampGain PgaPreampGain { get; set; }
    public required int PgaLadder { get; set; }
    public required bool Attenuator { get; set; }
    public required uint SampleRateHz { get; set; }
    public required uint MaxFrequency { get; set; }

    public BodePlotGpsdoStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            TestbenchException.TestbenchExceptionIfTrimDacZeroNotCalibrated(variables);
            TestbenchException.TestbenchExceptionIfTrimDacScaleNotCalibrated(variables);
            TestbenchException.TestbenchExceptionIfBufferInputVppNotCalibrated(variables);
            TestbenchException.TestbenchExceptionIfAdcBranchGainsNotCalibrated(variables);

            var resolution = AdcResolution.EightBit;

            Instruments.Instance.SetThunderscopeChannel(ChannelsEnabled);
            Instruments.Instance.SetThunderscopeResolution(resolution);
            Instruments.Instance.SetThunderscopeRate(SampleRateHz);
            Instruments.Instance.SetThunderscopeBranchGains(Frontend.GetAdcBranchGain(variables.Calibration, ChannelsEnabled, SampleRateHz));

            var pathCalibration = Utility.GetChannelPathCalibration(ChannelIndex, PgaPreampGain, PgaLadder, variables);
            var scaleFactor = Attenuator ? variables.Calibration.Frontend[ChannelIndex].AttenuatorScale : 1.0;
            var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
            var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
            Instruments.Instance.SetThunderscopeFrontend(ChannelIndex, new ThunderscopeChannelFrontendManualControl()
            {
                Coupling = ThunderscopeCoupling.AC,
                Termination = ThunderscopeTermination.FiftyOhm,
                Attenuator = Attenuator ? (byte)1 : (byte)0,
                DAC = trimDacZero,
                DPOT = pathCalibration.TrimDPot,
                PgaLadderAttenuation = pathCalibration.PgaLadder,
                PgaFilter = ThunderscopeBandwidth.BwFull,
                PgaHighGain = (PgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
            }, variables.FrontEndSettlingTimeMs);

            Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{ChannelIndex + 1}");

            var frequenciesHz = new List<uint>();
            uint startFrequencyHz = 1000;
            int pointsPerDecade = 20;
            var nyquistFrequency = SampleRateHz / 2;

            LBE142x gpsdo = new LBE142x();
            gpsdo.Connect();
            gpsdo.SetFrequencyTemporary(2, startFrequencyHz);
            gpsdo.SetPowerLevel(2, lowPower: false);
            gpsdo.SetOutputsEnabled(true);

            bool continueLoop = true;
            for (int d = 0; d < 10 && continueLoop; d++)
            {
                for (int i = 0; i < pointsPerDecade && continueLoop; i++)
                {
                    uint frequency = (uint)(startFrequencyHz * Math.Pow(10, d) * Math.Pow(10, (double)i / pointsPerDecade));
                    if (frequency > MaxFrequency)
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
            //if (!frequenciesHz.Contains(MaxFrequency))
            //{
            //    frequenciesHz.Add(MaxFrequency);
            //}
            //frequenciesHz.Add((uint)(startFrequencyHz * Math.Pow(10, decades)));

            var bodePoints = new Dictionary<uint, double>();

            gpsdo.SetFrequencyTemporary(2, frequenciesHz[0]); Thread.Sleep(200);
            var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, frequenciesHz[0], SampleRateHz, pathCalibration.BufferInputVpp, resolution, out float rangePercentAtRef) * scaleFactor;

            foreach (var frequencyHz in frequenciesHz)
            {
                cancellationToken.ThrowIfCancellationRequested();
                gpsdo.SetFrequencyTemporary(2, frequencyHz); Thread.Sleep(200);
                var frequencyToSearch = frequencyHz;
                if (frequencyHz > nyquistFrequency)
                {
                    frequencyToSearch = nyquistFrequency - (frequencyHz - nyquistFrequency);
                }
                var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, frequencyToSearch, SampleRateHz, pathCalibration.BufferInputVpp, resolution, out float rangePercent) * scaleFactor;

                // Normalise relative to the reference measurement
                double normalised = signalAtFrequency / signalAtRef;
                bodePoints[frequencyHz] = normalised;

                Logger.Instance.Log(LogLevel.Information, Index, Status.Running, $"Ch{ChannelIndex + 1}, frequency: {frequencyHz / 1e3:F3} kHz, scale: {normalised:F4}, ADC range {rangePercent:F0}%");
            }

            gpsdo.SetFrequencyTemporary(2, 10_000_000);
            gpsdo.Dispose();

            //var csv = bodePoints.Select(p => $"{p.Key},{p.Value:F4},{20.0 * Math.Log10(p.Value):F4}");
            //var csvString = "Frequency,Scale,dB\n" + string.Join("\n", csv);
            //File.WriteAllText($"{amplitude} Vpp - attenuator {attenuator}.csv", csvString);

            var tableMetadata =
            new ResultMetadataTable()
            {
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
                        ["ADC range (Fmin)", $"{rangePercentAtRef:F0}%"],
                        ["Points", bodePoints.Count.ToString()]]
            };

            var metadata =
                new ResultMetadataXYChart()
                {
                    ShowInReport = true,
                    Title = $"Overall gain vs. frequency",
                    XAxis = new ResultMetadataXYChartAxis { Label = "Frequency (Hz)", Scale = XYChartScaleType.Log10 },
                    YAxis = new ResultMetadataXYChartAxis { Label = "Gain (dB)", Scale = XYChartScaleType.Linear, AdditionalRangeValues = [-0.5, 0.5] },
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
