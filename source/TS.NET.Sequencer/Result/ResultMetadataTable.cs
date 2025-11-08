namespace TS.NET.Sequencer
{
    public class ResultMetadataTable : ResultMetadata
    {
        public string[] Headers { get; set; }
        public string[][] Rows { get; set; }
    }
}