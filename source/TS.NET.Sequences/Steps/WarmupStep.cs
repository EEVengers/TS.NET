using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class WarmupStep : Step
{
    private struct WarmupDataPoint
    {
        public double TimeSec { get; set; }
        public double FpgaTemp { get; set; }
    }

    public WarmupStep(string name, CommonVariables variables, int warmupTimeSec) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Logger.Instance.Log(LogLevel.Information, Index, $"Warming up for {warmupTimeSec} seconds");

            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            var startTime = DateTimeOffset.UtcNow;
            var datapoints = new List<WarmupDataPoint>();
            void AddDataPoint(double elapsedSec)
            {
                var temp = Instruments.Instance.GetThunderscopeFpgaTemp();

                datapoints.Add(new WarmupDataPoint()
                {
                    TimeSec = elapsedSec,
                    FpgaTemp = temp
                });
            }

            var nextIntervalTime = startTime.AddSeconds(5);
            while (true)
            {
                var time = DateTimeOffset.UtcNow;
                var elapsedSec = time.Subtract(startTime).TotalSeconds;
                if (elapsedSec >= warmupTimeSec)
                    break;
                cancellationToken.ThrowIfCancellationRequested();

                AddDataPoint(elapsedSec);

                // Calculate ms until next 5-second interval.
                // This logic aims for best effort alignment to 5 sec boundaries whilst ensuring the same number of points in the table with incorrect dT in adverse scenarios.
                double timeToIntervalSec = nextIntervalTime.Subtract(time).TotalSeconds;
                if (timeToIntervalSec < 0)
                    timeToIntervalSec = 0;
                nextIntervalTime = nextIntervalTime.AddSeconds(5);
                Task.Delay((int)(timeToIntervalSec * 1000), cancellationToken).Wait(cancellationToken);
            }
            // Last value
            AddDataPoint(DateTimeOffset.UtcNow.Subtract(startTime).TotalSeconds);

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
                        Data =  datapoints.Select(dp => new ResultMetadataXYChartPoint(){ X = Math.Round(dp.TimeSec, 0), Y = Math.Round(dp.FpgaTemp, 1) }).ToArray()
                    }
                ]
            });

            Result!.Summary = $"{datapoints.Last().FpgaTemp:F1}°C";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }
}
