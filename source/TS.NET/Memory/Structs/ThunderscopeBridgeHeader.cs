using System.Runtime.InteropServices;

namespace TS.NET
{
    // Ensure this is blitable (i.e. don't use bool)
    // Pack of 1 = No padding.

    // Memory mapped file layout:
    // [ThunderscopeBridgeHeader][DataRequestAndResponse byte][ThunderscopeBridgeDataRegionHeader][RegionA][ThunderscopeBridgeDataRegionHeader][RegionB]

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ThunderscopeBridgeHeader
    {
        internal byte Version;              // Allows UI to know which ThunderscopeMemoryBridgeHeader version to use, hence the size of the header.

        internal ThunderscopeBridgeConfig Bridge;           // 7 bytes, read only from UI perspective
        internal ThunderscopeHardwareConfig Hardware;       // 2 + 4*32, read only from UI perspective, UI uses SCPI interface to change configuration
        internal ThunderscopeProcessingConfig Processing;   // 37 bytes, read only from UI perspective, UI uses SCPI interface to change configuration
        internal ThunderscopeMonitoring Monitoring;         // 16 bytes, Read only from UI perspective, UI optionally displays these values     

        // BridgeConfig is set once from config file or hard coded
        // HardwareConfig, ProcessingConfig & DataMonitoring is runtime variable

        internal ThunderscopeMemoryAcquiringRegion AcquiringRegion; // Therefore 'AcquiredRegion' (to be used by UI) is the opposite
        //internal byte DataRequestAndResponse; // A byte after the header!
    }

    public enum ThunderscopeMemoryAcquiringRegion : byte
    {
        RegionA = 1,
        RegionB = 2
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeBridgeConfig
    {
        public ushort MaxChannelCount;
        public uint MaxChannelDataLength;
        public ThunderscopeChannelDataType ChannelDataType;
        public byte RegionCount;

        public unsafe readonly ulong DataRegionCapacityBytes()
        {
            return (ulong)sizeof(ThunderscopeBridgeDataRegionHeader) + MaxChannelCount * MaxChannelDataLength * ChannelDataType.Width();
        }

        public readonly ulong DataTotalCapacityBytes()
        {
            return DataRegionCapacityBytes() * RegionCount;
        }
    }
}