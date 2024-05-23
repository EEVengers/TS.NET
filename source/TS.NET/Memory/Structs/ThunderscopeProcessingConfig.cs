using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeProcessingConfig   // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        public ushort CurrentChannelCount;          // From 1 to ThunderscopeBridgeHeader.MaxChannelCount
        public ulong CurrentChannelDataLength;      // From 1 to ThunderscopeBridgeHeader.MaxChannelDataLength
        public HorizontalSumLength HorizontalSumLength;
        public TriggerChannel TriggerChannel;
        public TriggerMode TriggerMode;   
        public TriggerType TriggerType;
    }
}
