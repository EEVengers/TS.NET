namespace TS.NET
{
    public interface ITriggerResults { }

    public record struct EdgeTriggerResults(ulong[] ArmIndices, int ArmCount, ulong[] TriggerIndices, int TriggerCount, ulong[] CaptureEndIndices, int CaptureEndCount) : ITriggerResults;
}
