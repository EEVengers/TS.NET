namespace TS.NET
{
    public interface ITriggerResults { }

    public record struct EdgeTriggerResults(int[] ArmIndices, int ArmCount, int[] TriggerIndices, int TriggerCount, int[] CaptureEndIndices, int CaptureEndCount) : ITriggerResults;
}
