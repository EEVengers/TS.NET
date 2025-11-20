using System.Text.Json;
using System.Xml.Serialization;

namespace TS.NET.Sequencer
{
    public class ResultMetadataXYChart : ResultMetadata
    {
        public string? Title { get; set; }
        public required ResultMetadataXYChartAxis XAxis { get; set; }
        public required ResultMetadataXYChartAxis YAxis { get; set; }
        public required ResultMetadataXYChartSeries[] Series { get; set; }

        public string ToJson()
        {
            SetSeriesColours();
            return JsonSerializer.Serialize(this, DefaultCaseContext.Default.ResultMetadataXYChart);
        }

        public void SetSeriesColours()
        {
            string[] palette3 = ["#59008c", "#e90058", "#ffa600"];
            string[] palette4 = ["#59008c", "#c8006e", "#fd4940", "#ffa600"];
            string[] palette5 = ["#59008c", "#b30078", "#e90058", "#ff6233", "#ffa600"];
            string[] palette6 = ["#59008c", "#a4007e", "#d70066", "#f6334a", "#ff702b", "#ffa600"];
            string[] palette7 = ["#59008c", "#990081", "#c8006e", "#e90058", "#fd4940", "#ff7a25", "#ffa600"];
            string[] palette8 = ["#59008c", "#910083", "#bc0074", "#dd0062", "#f3284e", "#ff5839", "#ff8021", "#ffa600"];

            string[] selectedPalette = Series.Length switch
            {
                1 or 2 or 3 => palette3,
                4 => palette4,
                5 => palette5,
                6 => palette6,
                7 => palette7,
                8 => palette8,
                _ => throw new NotImplementedException("SetColours only supports 1 to 8 series")
            };

            for (int i = 0; i < Series.Length; i++)
            {
                Series[i].ColourHex = selectedPalette[i];
            }
        }
    }

    [XmlType("Axis")]
    public class ResultMetadataXYChartAxis
    {
        public required string Label { get; set; }
        public required XYChartScaleType Scale { get; set; }
        public double[]? AdditionalRangeValues { get; set; }      // Use this to coerce the axis range to include a specific value
    }

    [XmlType("Series")]
    public class ResultMetadataXYChartSeries
    {
        public required string Name { get; set; }
        public required string ColourHex { get; set; }
        public required ResultMetadataXYChartPoint[] Data { get; set; }
    }

    [XmlType("Point")]
    public class ResultMetadataXYChartPoint
    {
        public required double X { get; set; }
        public required double Y { get; set; }
    }

    public enum XYChartScaleType
    {
        Linear,
        Log10
    }
}
