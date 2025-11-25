using TS.NET.Sequencer;

namespace TS.NET.Testbench.UI;

public class LogUpdateDto : MessageDto
{
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string? Message { get; set; }
}
