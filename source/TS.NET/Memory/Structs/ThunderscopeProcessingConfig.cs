using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeProcessingConfig   // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        // If the contents of this struct changes, consider incrementing the BuildVersion on ThunderscopeBridgeHeader

        public ushort ChannelCount;     // From 1 to ThunderscopeBridgeHeader.MaxChannelCount
        public int ChannelDataLength;   // From 1 to ThunderscopeBridgeHeader.MaxChannelDataLength
        public ThunderscopeDataType ChannelDataType;

        public TriggerChannel TriggerChannel;   // U8
        public Mode Mode;         // U8
        public TriggerType TriggerType;         // U8
        public ulong TriggerDelayFs;
        public ulong TriggerHoldoffFs;
        public bool TriggerInterpolation;

        public long AutoTimeoutMs;

        public EdgeTriggerParameters EdgeTriggerParameters;
        public BurstTriggerParameters BurstTriggerParameters;

        public BoxcarAveraging BoxcarAveraging; // U32

        //public int ChannelLengthBytes()
        //{
        //    return ChannelDataLength * ChannelDataType.ByteWidth();
        //}
    }
}
