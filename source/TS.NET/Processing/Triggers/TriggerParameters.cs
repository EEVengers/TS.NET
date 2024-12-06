namespace TS.NET
{
    public interface ITriggerParameters { }

    public enum EdgeDirection { Rising, Falling, Any };

    public record struct EdgeTriggerParameters(int Level, uint Hysteresis, EdgeDirection Direction) : ITriggerParameters;

    public record struct BurstTriggerParameters(int WindowHighLevel, int WindowLowLevel, ulong MinimumQuietPeriod) : ITriggerParameters;
}
