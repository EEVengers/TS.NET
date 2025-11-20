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

            List<double> times = new List<double>();
            List<double> fpgaTemps = new List<double>();
            var startTime = DateTimeOffset.UtcNow;
            var nextIntervalTime = startTime.AddSeconds(5);
            while(true)
            {
                var time = DateTimeOffset.UtcNow;
                var elapsedSec = time.Subtract(startTime).TotalSeconds;
                if(elapsedSec >= variables.WarmupTimeSec)
                    break;
                cancellationToken.ThrowIfCancellationRequested();
                var temp = Instruments.Instance.GetThunderscopeFpgaTemp();
                times.Add(elapsedSec);
                fpgaTemps.Add(temp);

                // Calculate ms until next 5-second interval.
                // This logic aims for best effort alignment to 5 sec boundaries whilst ensuring the same number of points in the table with incorrect dT in adverse scenarios.
                double timeTointervalSec = nextIntervalTime.Subtract(time).TotalSeconds;
                if(timeTointervalSec < 0)
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
                        ColourHex = "#124567", 
                        Data = times.Zip(fpgaTemps, (x, y) => new ResultMetadataXYChartPoint(){ X = Math.Round(x, 0), Y = Math.Round(y, 2) }).ToArray()
                    }
                ]
            });
            Result!.Summary = $"FPGA: {fpgaTemps.Last():F2} °C";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, "Warmup complete");
            return Status.Done;
        };
    }
}
