namespace TS.NET;

public class NotImplementedTriggerI16 : ITriggerI16
{
    public void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff)
    {
    }

    public void Process(ReadOnlySpan<short> input, ulong sampleStartIndex, ref EdgeTriggerResults results)
    {
        results.ArmCount = 0;
        results.TriggerCount = 0;
        results.CaptureEndCount = 0;
    }
}
