using TS.NET.Sequencer;

namespace TS.NET.Testbench.UI;

public class LogDto : MessageDto
{
    public required LogEvent[] Log { get; set; }

    internal static LogDto FromLog()
    {
        return new LogDto()
        {
            Type = "log",
            Log = Logger.Instance.LogHistory.ToArray()
        };
    }
}
