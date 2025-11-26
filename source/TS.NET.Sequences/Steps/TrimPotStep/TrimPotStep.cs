using System.Reflection;
using System.Text;
using System.Text.Json;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimPotStep : ModalUiStep
{
    public required int ChannelIndex { get; set; }
    public required int[] ChannelsEnabled { get; set; }
    public PgaPreampGain PgaPreampGain { get; set; }
    public int PgaLadder { get; set; }
    public bool Attenuator { get; set; }
    public required uint SampleRateHz { get; set; }

    private Status status = Status.Running;
    private bool continueLoop = false;

    public TrimPotStep(string name, ModalUiContext modalUiContext, CommonVariables variables) : base(name, modalUiContext)
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
            double attenuatorScale = ChannelIndex switch
            {
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

            uint[] frequenciesHz = [500, 1_000, 2_000, 4_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 990_000];

            continueLoop = true;
            while (continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bodePoints = new Dictionary<uint, double>();
                SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, frequenciesHz[0]);
                var signalAtRef = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, frequenciesHz[0], SampleRateHz, pathCalibration.BufferInputVpp, resolution) / (Attenuator ? attenuatorScale : 1.0);
                bodePoints[frequenciesHz[0]] = 1.0;
                for (int i = 1; i < frequenciesHz.Length && continueLoop; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SigGens.Instance.SetSdgParameterFrequency(ChannelIndex, frequenciesHz[i]);
                    var signalAtFrequency = Instruments.Instance.GetThunderscopeVppAtFrequencyLsq(ChannelIndex, frequenciesHz[i], SampleRateHz, pathCalibration.BufferInputVpp, resolution) / (Attenuator ? attenuatorScale : 1.0);
                    // Normalise relative to the reference measurement
                    double normalised = signalAtFrequency / signalAtRef;
                    bodePoints[frequenciesHz[i]] = normalised;
                }

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

                UpdateUi<TrimPot>(new Dictionary<string, object?>()
                {
                    { "Title", "Channel 1 - Trim pot"},
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