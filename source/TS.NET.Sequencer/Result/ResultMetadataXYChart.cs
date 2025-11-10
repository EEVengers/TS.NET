namespace TS.NET.Sequencer
{
    public class ResultMetadataXYChart : ResultMetadata
    {
        public required string Title { get; set; }
        public required string XAxisTitle { get; set; }
        public required string YAxisTitle { get; set; }
        public required double[] X { get; set; }
        public required double[] Y { get; set; }
        public XYChartScaleType XScaleType { get; set; }
        public XYChartScaleType YScaleType { get; set; }
    }

    public enum XYChartScaleType
    {
        Linear,
        Log10
    }
}
