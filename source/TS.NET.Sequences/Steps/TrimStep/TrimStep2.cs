using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimStep2 : ModalUiStep
{
    public required int ChannelIndex { get; set; }
    public required int[] ChannelsEnabled { get; set; }
    public required PgaPreampGain PgaPreampGain { get; set; }
    public required int PgaLadder { get; set; }
    public required bool Attenuator { get; set; }
    public required uint SampleRateHz { get; set; }
    public required FactoryTrimArbProfile ArbProfile { get; set; }
    public required double ArrowX { get; set; }
    public required double ArrowY { get; set; }

    private Status status = Status.Running;
    private bool continueLoop = true;

    public TrimStep2(string name, ModalUiContext modalUiContext, FactoryVariables variables) : base(name, modalUiContext)
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

            SigGens.Instance.SetSdgLoad(ChannelIndex, ThunderscopeTermination.FiftyOhm);
            SigGens.Instance.SetSdgParameterAmplitude(ChannelIndex, amplitudeVpp);
            SigGens.Instance.SetSdgParameterOffset(ChannelIndex, 0);

            SigGens.Instance.SetSdgArbitraryBurstList(ChannelIndex, amplitudeVpp, ArbProfile!.SampleRateHz, waveName: ArbProfile.Name);
            SigGens.Instance.SetSdgChannel([ChannelIndex]);

            var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
            var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
            Instruments.Instance.SetThunderscopeCalManual50R(ChannelIndex, Attenuator, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, ThunderscopeBandwidth.BwFull, variables.FrontEndSettlingTimeMs);

            var captureSamplesAtScopeRate = ((double)ArbProfile.Length / ArbProfile.SampleRateHz) * SampleRateHz;
            var length = checked((int)(captureSamplesAtScopeRate * 1.1));
            var bufferArray = new sbyte[length];
            Span<sbyte> buffer = bufferArray;

            int loopCount = 0;

            while (continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bodePoints = new Dictionary<double, double>();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        Instruments.Instance.GetSingleRisingEdgeTriggeredCaptureI8((TriggerChannel)(ChannelIndex + 1), bufferArray.Length, 1, buffer, pathCalibration, triggerLevelV: 0.0f, onAfterFirstRead: () =>
                        {
                            SigGens.Instance.TriggerSdgManualBurst(ChannelIndex);
                        });
                        break;
                    }
                    catch { }
                }
                var stopwatch = Stopwatch.StartNew();
                // Split captured burst list by rising-edge zero crossings using an arm level based on hysteresis.
                var expectedChunkCount = ArbProfile.Frequencies.Length;
                var requiredCrossingCount = (expectedChunkCount * ArbProfile.CyclesPerFrequency);
                long sum = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    sum += buffer[i];
                }
                sbyte triggerLevel = 0;
                const double hysteresisPercent = 20.0;
                var hysteresisCounts = Math.Max(1, (int)Math.Round(sbyte.MaxValue * (hysteresisPercent / 100.0)));
                var armLevel = (sbyte)Math.Clamp(triggerLevel - hysteresisCounts, sbyte.MinValue, sbyte.MaxValue);

                var risingZeroCrossings = new List<int>() { 0 };
                var armed = false;
                for (int i = 0; i < buffer.Length; i++)
                {
                    var value = buffer[i];
                    if (!armed)
                    {
                        if (value <= armLevel)
                            armed = true;
                    }
                    else if (value > triggerLevel)
                    {
                        risingZeroCrossings.Add(i);
                        armed = false;
                    }
                }

                if (risingZeroCrossings.Count < requiredCrossingCount)
                {
                    throw new TestbenchException($"Only found {risingZeroCrossings.Count} rising zero crossings, expected at least {requiredCrossingCount}.");
                }

                var chunkSpans = new List<ArraySegment<sbyte>>(expectedChunkCount);
                for (int chunkIndex = 0; chunkIndex < expectedChunkCount; chunkIndex++)
                {
                    var startCrossingIndex = chunkIndex * ArbProfile.CyclesPerFrequency;
                    var endCrossingIndex = (chunkIndex + 1) * ArbProfile.CyclesPerFrequency;
                    var start = risingZeroCrossings[startCrossingIndex];
                    var end = risingZeroCrossings[endCrossingIndex];
                    if (end <= start)
                        throw new TestbenchException($"Invalid chunk bounds at index {chunkIndex}: start={start}, end={end}.");

                    chunkSpans.Add(new ArraySegment<sbyte>(bufferArray, start, end - start));
                }

                var signalAtRef = Instruments.Instance.GetPeakPeakAtFrequencyLsq(chunkSpans[0].AsSpan(), ArbProfile.Frequencies[0], SampleRateHz, out int rangeAtRef, saturationCheck: false);
                var rangePercentAtRef = rangeAtRef / 256.0f * 100.0f;
                Result!.Summary = $"ADC range: {rangePercentAtRef:F0}%";
                Debug.WriteLine($"ADC range: {rangePercentAtRef:F0}%");
                bodePoints[ArbProfile.Frequencies[0]] = 1.0;
                for (int i = 1; i < ArbProfile.Frequencies.Length && continueLoop; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var signalAtFrequency = Instruments.Instance.GetPeakPeakAtFrequencyLsq(chunkSpans[i].AsSpan(), ArbProfile.Frequencies[i], SampleRateHz, out int rangeAtFrequency, saturationCheck: false);
                    // Normalise relative to the reference measurement
                    double normalised = signalAtFrequency / signalAtRef;
                    bodePoints[ArbProfile.Frequencies[i]] = normalised;

                }

                stopwatch.Stop();
                Debug.WriteLine($"Bode measurement loop took {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                var metadata =
                    new ResultMetadataXYChart()
                    {
                        ShowInReport = true,
                        Title = $"Overall gain vs. frequency [{loopCount}]",
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
                    { "Chart", metadata },
                    { "Image", "frontends.png" },
                    { "ArrowX", ArrowX },
                    { "ArrowY", ArrowY }
                });

                Thread.Sleep(100);
                loopCount++;
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