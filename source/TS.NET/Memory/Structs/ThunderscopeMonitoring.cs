using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeProcessingMonitoring
    {
        // If the contents of this struct changes, consider incrementing the BuildVersion on ThunderscopeBridgeHeader

        public ulong BridgeWrites;
        public ulong BridgeReads;
        public float BridgeWritesPerSec;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeMonitoring
    {
        // If the contents of this struct changes, consider incrementing the BuildVersion on ThunderscopeBridgeHeader

        //public ThunderscopeHardwareMonitoring Hardware;
        public ThunderscopeProcessingMonitoring Processing;
    }
}
