namespace TS.NET
{
    public interface ITriggerParameters { }

    public enum EdgeDirection { Rising, Falling, Any };

    public record struct EdgeTriggerParameters(float LevelV, float HysteresisPercent, EdgeDirection Direction) : ITriggerParameters;

    public record struct BurstTriggerParameters(int WindowHighLevel, int WindowLowLevel, long MinimumInRangePeriod) : ITriggerParameters;
}
