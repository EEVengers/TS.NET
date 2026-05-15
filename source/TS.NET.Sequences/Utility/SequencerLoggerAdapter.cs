using Microsoft.Extensions.Logging;

namespace TS.NET.Sequences;

internal sealed class SequencerLoggerAdapter(int stepIndex) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return EmptyScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message = string.IsNullOrEmpty(message) ? exception.ToString() : $"{message}{Environment.NewLine}{exception}";
        }

        var mappedLevel = MapLevel(logLevel);
        Sequencer.Logger.Instance.Log(mappedLevel, stepIndex, message);
    }

    private static Sequencer.LogLevel MapLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => Sequencer.LogLevel.Trace,
            LogLevel.Debug => Sequencer.LogLevel.Debug,
            LogLevel.Information => Sequencer.LogLevel.Information,
            LogLevel.Warning => Sequencer.LogLevel.Warning,
            LogLevel.Error => Sequencer.LogLevel.Error,
            LogLevel.Critical => Sequencer.LogLevel.Critical,
            _ => Sequencer.LogLevel.None,
        };
    }

    private sealed class EmptyScope : IDisposable
    {
        internal static readonly EmptyScope Instance = new();

        public void Dispose()
        {
        }
    }
}
