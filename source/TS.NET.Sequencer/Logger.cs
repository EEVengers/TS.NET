namespace TS.NET.Sequencer;

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6,
}

public class LogEvent
{
    public LogLevel Level { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string Message { get; set; }
}

public sealed class Logger
{
    private static readonly Lazy<Logger> lazy = new(() => new Logger());
    public static Logger Instance { get { return lazy.Value; } }

    public List<LogEvent> LogHistory { get; set; } = [];
    public Action<LogEvent>? EventLogged;

    private Logger() { }

    public void Log(LogLevel level, string message)
    {
        var log = new LogEvent() { Level = level, Timestamp = DateTimeOffset.Now, Message = message };
        LogHistory.Add(log);
        EventLogged?.Invoke(log);
    }

    public void Log(LogLevel level, int stepIndex, Status status, string message)
    {
        var logMessage = $"Step {stepIndex} | {status} | {message}";
        var log = new LogEvent() { Level = level, Timestamp = DateTimeOffset.Now, Message = logMessage };
        LogHistory.Add(log);
        EventLogged?.Invoke(log);
    }

    public void Log(LogLevel level, int stepIndex, Status status)
    {
        var logMessage = $"Step {stepIndex} | {status}";
        var log = new LogEvent() { Level = level, Timestamp = DateTimeOffset.Now, Message = logMessage };
        LogHistory.Add(log);
        EventLogged?.Invoke(log);
    }

    public void Log(LogLevel level, int stepIndex, string message)
    {
        var logMessage = $"Step {stepIndex} | {message}";
        var log = new LogEvent() { Level = level, Timestamp = DateTimeOffset.Now, Message = logMessage };
        LogHistory.Add(log);
        EventLogged?.Invoke(log);
    }
}
