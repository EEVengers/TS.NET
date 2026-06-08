using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimStep : ModalUiStep
{
    public required int ChannelIndex { get; set; }
    public required int[] ChannelsEnabled { get; set; }
    public PgaPreampGain PgaPreampGain { get; set; }
    public int PgaLadder { get; set; }
    public bool Attenuator { get; set; }
    public required uint SampleRateHz { get; set; }
    public required uint[] BodeFrequencies { get; set; }

    private Status status = Status.Running;
    private bool continueLoop = false;

    public TrimStep(string name, ModalUiContext modalUiContext, CommonVariables variables) : base(name, modalUiContext)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            RegisterEventHandler((JsonElement eventData) =>
            {
                if (eventData.TryGetProperty("buttonClicked", out var buttonClicked))
                {
                    switch (buttonClicked.GetString())
                    {
                        case "ok":
                        case "yes":
                            status = Status.Done;
                            break;
                        case "cancel":
                        case "no":
                            status = Status.Cancelled;
                            break;

                    }
                    continueLoop = false;
                }
            });

            var resolution = AdcResolution.EightBit;
            Instruments.Instance.SetThunderscopeChannel(ChannelsEnabled);
            Instruments.Instance.SetThunderscopeResolution(resolution);
            Instruments.Instance.SetThunderscopeRate(SampleRateHz);
            var pathCalibration = Utility.GetChannelPathCalibration(ChannelIndex, PgaPreampGain, PgaLadder, variables);
            var scaleFactor = Attenuator ? variables.Calibration.Frontend[ChannelIndex].AttenuatorScale : 1.0;
            double amplitudeVpp = pathCalibration.BufferInputVpp * 0.8 * scaleFactor;
            if (amplitudeVpp > 5.0)
            {
                amplitudeVpp = 5.0;
            }
            SigGens.Instance.SetSdgChannel([ChannelIndex]);
            SigGens.Instance.SetSdgLoad(ChannelIndex, ThunderscopeTermination.FiftyOhm);
            SigGens.Instance.SetSdgSine(ChannelIndex);
            SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, 1000);
            SigGens.Instance.SetSdgParameterAmplitude(ChannelIndex, amplitudeVpp);
            SigGens.Instance.SetSdgParameterOffset(ChannelIndex, 0);
            var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
            var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
            Instruments.Instance.SetThunderscopeCalManual50R(ChannelIndex, Attenuator, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, ThunderscopeBandwidth.BwFull, variables.FrontEndSettlingTimeMs);

            continueLoop = true;
            while (continueLoop)
            {
                var stopwatch = Stopwatch.StartNew();
                cancellationToken.ThrowIfCancellationRequested();

                var bodePoints = new Dictionary<uint, double>();
                SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, BodeFrequencies[0]);
                var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, BodeFrequencies[0], SampleRateHz, pathCalibration.BufferInputVpp, resolution, out float rangePercentAtRef) * scaleFactor;
                bodePoints[BodeFrequencies[0]] = 1.0;
                for (int i = 1; i < BodeFrequencies.Length && continueLoop; i++)
                {                   
                    cancellationToken.ThrowIfCancellationRequested();
                    SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, BodeFrequencies[i]);
                    var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, BodeFrequencies[i], SampleRateHz, pathCalibration.BufferInputVpp, resolution, out float rangePercent) * scaleFactor;
                    // Normalise relative to the reference measurement
                    double normalised = signalAtFrequency / signalAtRef;
                    bodePoints[BodeFrequencies[i]] = normalised;

                }
                stopwatch.Stop();
                Debug.WriteLine($"Bode measurement loop took {stopwatch.Elapsed.TotalSeconds:F2} seconds");
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

                UpdateUi<Trim>(new Dictionary<string, object?>()
                {
                    { "Title", name},
                    { "Chart", metadata }
                });

                Thread.Sleep(100);
            }

            HideUi();

            return status;
        };
    }

    public static string GetChartJs()
    {
        var sb = new StringBuilder();
        using var stream1 = typeof(Sequence).Assembly.GetManifestResourceStream("TS.NET.Sequencer.HTML.d3.v7.min.js");
        if (stream1 != null)
        {
            using var reader = new StreamReader(stream1);
            sb.Append(reader.ReadToEnd());
        }

        using var stream2 = typeof(Sequence).Assembly.GetManifestResourceStream("TS.NET.Sequencer.HTML.ResultMetadataXYChart.js");
        if (stream2 != null)
        {
            sb.AppendLine("");
            using var reader = new StreamReader(stream2);
            sb.Append(reader.ReadToEnd());
        }

        return sb.ToString();
    }

    public static string GetChartStyle()
    {
        var sb = new StringBuilder();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.HTML.ResultMetadataXYChart.css");
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            sb.Append(reader.ReadToEnd());
        }
        return sb.ToString();
    }

}