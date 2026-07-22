namespace TS.NET
{
    public interface ITriggerParameters { }

    public enum EdgeDirection { Rising, Falling, Any };

    public enum BurstEdgeDirection { Rising, Falling };

    public record struct TriggerChannelParameters(ulong SampleRateHz, float TriggerChannelVpp, float TriggerChannelOffsetV);

    public record struct EdgeTriggerParameters(float LevelV, EdgeDirection Direction, float HysteresisPercent) : ITriggerParameters;

    public record struct BurstTriggerParameters(float LevelV, BurstEdgeDirection Direction, float HysteresisPercent, float QuietHighLevelV, float QuietLowLevelV, long QuietTimeFs) : ITriggerParameters;

    //public record struct WindowTriggerParameters(int UpperLevel, int LowerLevel) : ITriggerParameters;

    //public record struct RuntTriggerParameters(int UpperLevel, int LowerLevel, long MinimumWidth, long MaximumWidth) : ITriggerParameters;

    //public record struct WidthTriggerParameters(int Level, int Hysteresis, EdgeDirection Polarity, long MinimumWidth, long MaximumWidth) : ITriggerParameters;

    //public record struct IntervalTriggerParameters(int Level, int Hysteresis, EdgeDirection Direction, long MinimumInterval, long MaximumInterval) : ITriggerParameters;

    //public record struct DropoutTriggerParameters(int Level, int Hysteresis, EdgeDirection Direction, long TimeoutPeriod) : ITriggerParameters;

    //public record struct SlewRateTriggerParameters(int Level, int Hysteresis, EdgeDirection Direction, long MinimumTime, long MaximumTime) : ITriggerParameters;
}
