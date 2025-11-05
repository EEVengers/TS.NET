using System.Xml.Serialization;

namespace TS.NET.Sequencer;

[Serializable]
public class Result
{
    public Status? Status { get; set; }
    public TimeSpan? Duration { get; set; }
    [XmlIgnore] public Exception? Exception { get; set; }
    public string? Summary { get; set; }
    [XmlArrayItem(typeof(ResultMetadataTable))]
    [XmlArrayItem(typeof(ResultMetadataChart))]
    public ResultMetadata[]? Metadata { get; set; }
}
