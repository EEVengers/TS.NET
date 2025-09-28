namespace TS.NET.Calibration.UI
{
    public class LogUpdateDto : MessageDto
    {
        public DateTimeOffset Timestamp { get; set; }
        public Sequencer.LogLevel Level { get; set; }
        public string? Message { get; set; }
    }
}
