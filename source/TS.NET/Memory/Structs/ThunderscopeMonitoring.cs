using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeProcessingMonitoring
    {
        public ulong BridgeWrites;
        public ulong BridgeReads;
        public float BridgeWritesPerSec;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeMonitoring
    {
        //public ThunderscopeHardwareMonitoring Hardware;
        public ThunderscopeProcessingMonitoring Processing;
    }
}
