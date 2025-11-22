using CommunityToolkit.HighPerformance.Buffers;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class NsdStep : Step
{
    public double MinLimit { get; set; } = double.MinValue;
    public double MaxLimit { get; set; } = double.MaxValue;

    public NsdStep(string name, int channelIndex, int pathIndex, ThunderscopeTermination termination, ThunderscopeBandwidth bandwidth, uint rateHz, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            switch (termination)
            {
                case ThunderscopeTermination.FiftyOhm:
                    Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, false, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, bandwidth, variables.FrontEndSettlingTimeMs);
                    break;
                case ThunderscopeTermination.OneMegaohm:
                    Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, false, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, bandwidth, variables.FrontEndSettlingTimeMs);
                    break;
            }

            int sampleCount = 50_000_000;
            using SpanOwner<sbyte> i8Buffer = SpanOwner<sbyte>.Allocate(sampleCount);           // Returned to pool when it goes out of scope
            Instruments.Instance.GetChannelDataI8(channelIndex, sampleCount, i8Buffer.Span);
            using MemoryOwner<double> f64Buffer = MemoryOwner<double>.Allocate(sampleCount);
            

            Widen.I8toF64(i8Buffer.Span, f64Buffer.Span);
            //var spectrum = Nsd.Linear(f64Buffer.Memory, rateHz, outputWidth: 131072);
            //var spectrum = Nsd.DualLinear(f64Buffer.Memory, rateHz, maxWidth: 262144, minWidth: 4096);
            var spectrum = Nsd.StackedLinear(f64Buffer.Memory, rateHz, maxWidth: 262144, minWidth: 4096);
            //var spectrum = Nsd.Log(f64Buffer.Memory, rateHz, 20_000, 1_000_000_000, 10, 30, 8192, 2.0);

            var points = new List<ResultMetadataXYChartPoint>();
            for (int i = 0; i < spectrum.Frequencies.Span.Length; i++)
            {
                points.Add(new ResultMetadataXYChartPoint() { X = spectrum.Frequencies.Span[i], Y = spectrum.Values.Span[i] * (pathCalibration.BufferInputVpp / 256.0) });
            }

            var chartMetadata =
                new ResultMetadataXYChart()
                {
                    ShowInReport = true,
                    Title = $"Noise spectral density",
                    XAxis = new ResultMetadataXYChartAxis { Label = "Frequency (Hz)", Scale = XYChartScaleType.Log10, AdditionalRangeValues = [10e3, 1e9] },
                    YAxis = new ResultMetadataXYChartAxis { Label = "Noise (V/rHz)", Scale = XYChartScaleType.Log10, AdditionalRangeValues = [100e-9, 100e-12] },
                    Series =
                    [
                        new ResultMetadataXYChartSeries
                        {
                            Name = Name,
                            ColourHex = "",
                            Data = points.ToArray()
                        }
                    ]
                };
            Result!.Metadata!.Add(chartMetadata);

            return Status.Done;
        };
    }
}
