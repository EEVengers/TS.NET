namespace TS.NET
{
    public interface ITriggerI8
    {
        void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff);
        void Process(ReadOnlySpan<sbyte> input, Span<int> captureEndIndices, out int captureEndCount);
    }
}