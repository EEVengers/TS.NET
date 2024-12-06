namespace TS.NET
{
    public interface ITriggerI8
    {
        void SetHorizontal(ulong windowWidth, ulong windowTriggerPosition, ulong additionalHoldoff);
        void Process(ReadOnlySpan<sbyte> input, Span<uint> captureEndIndices, out uint captureEndCount);
    }
}