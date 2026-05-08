using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class WarmupStep : Step
{
    public WarmupStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Logger.Instance.Log(LogLevel.Information, Index, $"Warming up for {variables.WarmupTimeSec} seconds");
            //Task.Delay(variables.WarmupTimeSec * 1000, cancellationToken).Wait(cancellationToken);

            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);
            double GetChannelAverage(int channelIndex, ThunderscopeChannelPathCalibration pathCalibration)
            {
                Instruments.Instance.SetThunderscopeChannel([channelIndex]);
                Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables.FrontEndSettlingTimeMs);
                return Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000) * (pathCalibration.BufferInputVpp / 256.0);
            }

            List<double> times = new List<double>();
            List<double> fpgaTemps = new List<double>();
            List<double> channel1Offsets = new List<double>();
            List<double> channel2Offsets = new List<double>();
            List<double> channel3Offsets = new List<double>();
            List<double> channel4Offsets = new List<double>();

            var startTime = DateTimeOffset.UtcNow;
            var nextIntervalTime = startTime.AddSeconds(5);
            while (true)
            {
                var time = DateTimeOffset.UtcNow;
                var elapsedSec = time.Subtract(startTime).TotalSeconds;
                if (elapsedSec >= variables.WarmupTimeSec)
                    break;
                cancellationToken.ThrowIfCancellationRequested();
                var temp = Instruments.Instance.GetThunderscopeFpgaTemp();
                times.Add(elapsedSec);
                fpgaTemps.Add(temp);
                channel1Offsets.Add(GetChannelAverage(0, variables.Calibration.Channel1.Paths[0]));
                channel2Offsets.Add(GetChannelAverage(1, variables.Calibration.Channel2.Paths[0]));
                channel3Offsets.Add(GetChannelAverage(2, variables.Calibration.Channel3.Paths[0]));
                channel4Offsets.Add(GetChannelAverage(3, variables.Calibration.Channel4.Paths[0]));

                // Calculate ms until next 5-second interval.
                // This logic aims for best effort alignment to 5 sec boundaries whilst ensuring the same number of points in the table with incorrect dT in adverse scenarios.
                double timeTointervalSec = nextIntervalTime.Subtract(time).TotalSeconds;
                if (timeTointervalSec < 0)
                    timeTointervalSec = 0;
                nextIntervalTime = nextIntervalTime.AddSeconds(5);
                Task.Delay((int)(timeTointervalSec * 1000), cancellationToken).Wait(cancellationToken);
            }
            // Last temperature value
            times.Add(DateTimeOffset.UtcNow.Subtract(startTime).TotalSeconds);
            fpgaTemps.Add(Instruments.Instance.GetThunderscopeFpgaTemp());

            Result!.Metadata!.Add(new ResultMetadataXYChart()
            {
                ShowInReport = true,
                Title = "FPGA temperature vs. time",
                XAxis = new ResultMetadataXYChartAxis()
                {
                    Label = "Time (s)",
                    Scale = XYChartScaleType.Linear
                },
                YAxis = new ResultMetadataXYChartAxis()
                {
                    Label = "Temperature (°C)",
                    Scale = XYChartScaleType.Linear,
                    AdditionalRangeValues = [30.0, 80.0]
                },
                Series =
                [
                    new ResultMetadataXYChartSeries(){
                        Name = "FPGA temperature",
                        ColourHex = "",
                        Data = times.Zip(fpgaTemps, (x, y) => new ResultMetadataXYChartPoint(){ X = Math.Round(x, 0), Y = Math.Round(y, 1) }).ToArray()
                    }
                ]
            });

            Result!.Metadata!.Add(new ResultMetadataXYChart()
            {
                ShowInReport = true,
                Title = "Channel offset vs. FPGA temperature",
                LegendLocation = ResultMetadataXYChartLegendLocation.TopLeft,
                XAxis = new ResultMetadataXYChartAxis()
                {
                    Label = "Temperature (°C)",
                    Scale = XYChartScaleType.Linear,
                    AdditionalRangeValues = [30.0, 80.0]
                },
                YAxis = new ResultMetadataXYChartAxis()
                {
                    Label = "Offset (mV)",
                    Scale = XYChartScaleType.Linear
                },
                Series =
                [
                    new ResultMetadataXYChartSeries(){
                        Name = "Channel 1 (HG L0)",
                        ColourHex = "",
                        Data = fpgaTemps.Zip(channel1Offsets, (x, y) => new ResultMetadataXYChartPoint(){ X = Math.Round(x, 1), Y = Math.Round(y*1000, 1) }).ToArray()
                    },
                    new ResultMetadataXYChartSeries(){
                        Name = "Channel 2 (HG L0)",
                        ColourHex = "",
                        Data = fpgaTemps.Zip(channel2Offsets, (x, y) => new ResultMetadataXYChartPoint(){ X = Math.Round(x, 1), Y = Math.Round(y*1000, 1) }).ToArray()
                    },
                    new ResultMetadataXYChartSeries(){
                        Name = "Channel 3 (HG L0)",
                        ColourHex = "",
                        Data = fpgaTemps.Zip(channel3Offsets, (x, y) => new ResultMetadataXYChartPoint(){ X = Math.Round(x, 1), Y = Math.Round(y*1000, 1) }).ToArray()
                    },
                    new ResultMetadataXYChartSeries(){
                        Name = "Channel 4 (HG L0)",
                        ColourHex = "",
                        Data = fpgaTemps.Zip(channel4Offsets, (x, y) => new ResultMetadataXYChartPoint(){ X = Math.Round(x, 1), Y = Math.Round(y*1000, 1) }).ToArray()
                    }
                ]
            });

            Result!.Summary = $"FPGA: {fpgaTemps.Last():F1} °C";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }
}
