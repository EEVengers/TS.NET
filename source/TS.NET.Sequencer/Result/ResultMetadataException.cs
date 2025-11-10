using System.Xml.Serialization;

namespace TS.NET.Sequencer;

public class ResultMetadataException : ResultMetadata
{
    [XmlIgnore] public required Exception Exception { get; set; }
}