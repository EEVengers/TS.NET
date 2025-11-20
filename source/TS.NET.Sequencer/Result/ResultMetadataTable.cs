namespace TS.NET.Sequencer
{
    public class ResultMetadataTable : ResultMetadata
    {
        public required string? Name { get; set; }
        public required string[] Headers { get; set; }
        public required string[][] Rows { get; set; }
    }
}