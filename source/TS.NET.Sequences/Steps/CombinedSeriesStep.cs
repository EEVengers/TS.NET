using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class CombinedSeriesStep : Step
{
    public required string ChartTitle { get; set; }
    public required ResultMetadataXYChartAxis ChartXAxis { get; set; }
    public required ResultMetadataXYChartAxis ChartYAxis { get; set; }
    public required SeriesReference[] ChartSeries { get; set; }
    public ResultMetadataXYChartLegendLocation ChartLegendLocation { get; set; } = ResultMetadataXYChartLegendLocation.TopRight;

    public CombinedSeriesStep(string name, Sequence sequence) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if(ChartXAxis == null)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, "ChartXAxis is not set");
                return Status.Error;
            }
            if(ChartYAxis == null)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, "ChartYAxis is not set");
                return Status.Error;
            }
            if(ChartSeries == null || ChartSeries.Length == 0)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, "ChartSeries is not set");
                return Status.Error;
            }
            var series = new List<ResultMetadataXYChartSeries>();
            foreach(var seriesDefinition in ChartSeries)
            {
                series.Add(((ResultMetadataXYChart)sequence.Steps!.First(s => s.Name == seriesDefinition.StepName).Result!.Metadata!.First(r => r.GetType() == typeof(ResultMetadataXYChart))).Series.First());
            }
            var metadata =
                new ResultMetadataXYChart()
                {
                    ShowInReport = true,
                    Title = ChartTitle,
                    XAxis = ChartXAxis,
                    YAxis = ChartYAxis,
                    Series = series.ToArray(),
                    LegendLocation = ChartLegendLocation,
                };

            Result!.Metadata!.Add(metadata);
            return Status.Done;
        };
    }
}

public class SeriesReference
{
    public required string StepName {get;set;}
}