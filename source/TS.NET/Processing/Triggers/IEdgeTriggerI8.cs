namespace TS.NET
{
    public interface IEdgeTriggerI8
    {
        void SetVertical(sbyte triggerLevel, byte triggerHysteresis);
        void SetHorizontal(ulong windowWidth, ulong windowTriggerPosition, ulong additionalHoldoff);
        void Process(ReadOnlySpan<sbyte> input, Span<uint> captureEndIndices, out uint captureEndCount);
    }
}