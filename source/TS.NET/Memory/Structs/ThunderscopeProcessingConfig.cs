using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeProcessingConfig   // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        public ushort CurrentChannelCount;      // From 1 to ThunderscopeBridgeHeader.MaxChannelCount
        public ulong CurrentChannelDataLength;  // From 1 to ThunderscopeBridgeHeader.MaxChannelDataLength
               
        public TriggerChannel TriggerChannel;   // U8
        public TriggerMode TriggerMode;         // U8
        public TriggerType TriggerType;         // U8
        public ulong TriggerDelayFs;
        public ulong TriggerHoldoff;
        public int TriggerLevel;                // I32 (allows for 32-bit processing)
        public uint TriggerHysteresis;          // U32 (allows for 32-bit processing)

        public BoxcarAveraging BoxcarAveraging; // U32
    }
}
