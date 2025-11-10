using System.Xml.Serialization;

namespace TS.NET.Sequencer;

[Serializable]
public class Result
{
    public Status? Status { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Summary { get; set; }
    [XmlArrayItem(typeof(ResultMetadataException))]
    [XmlArrayItem(typeof(ResultMetadataTable))]
    [XmlArrayItem(typeof(ResultMetadataXYChart))]
    public List<ResultMetadata>? Metadata { get; set; }
}
