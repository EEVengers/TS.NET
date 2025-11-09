using TS.NET;

public interface ITriggerI16
{
    void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff);
    void Process(ReadOnlySpan<short> input, ulong sampleStartIndex, ref EdgeTriggerResults results);
}