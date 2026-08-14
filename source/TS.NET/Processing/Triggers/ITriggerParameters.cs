namespace TS.NET
{
    public interface ITriggerParameters { }

    public enum EdgeDirection { Rising, Falling, Any };

    public enum BurstEdgeDirection { Rising, Falling };

    public enum WindowDirection { Enter, Exit };

    public record struct TriggerChannelParameters(ulong SampleRateHz, float TriggerChannelVpp, float TriggerChannelOffsetV);

    public record struct EdgeTriggerParameters(float LevelV, EdgeDirection Direction, float HysteresisPercent) : ITriggerParameters;

    public record struct WindowTriggerParameters(float UpperLevelV, float LowerLevelV, WindowDirection Direction, float HysteresisPercent) : ITriggerParameters;

    public record struct BurstTriggerParameters(float LevelV, BurstEdgeDirection Direction, float HysteresisPercent, float QuietUpperLevelV, float QuietLowerLevelV, long QuietTimeFs) : ITriggerParameters;
}
