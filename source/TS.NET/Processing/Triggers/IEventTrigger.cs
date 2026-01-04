namespace TS.NET;

public interface IEventTrigger
{
    void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff);
    void EnqueueEvent(ulong sampleIndex);
    void Process(int inputLength, ulong sampleStartIndex, ref EventTriggerResults results);
}