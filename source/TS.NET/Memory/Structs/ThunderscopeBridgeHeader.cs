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
        public const uint BuildVersion = 1; // If the build version of the writer & reader has a mismatch, throw an error
        internal uint Version;              // The version used by the writer

        internal ThunderscopeBridgeConfig Bridge;           // 7 bytes, read only from UI perspective
        internal ThunderscopeHardwareConfig Hardware;       // 2 + 4*32, read only from UI perspective, UI uses SCPI interface to change configuration
        internal ThunderscopeProcessingConfig Processing;   // 37 bytes, read only from UI perspective, UI uses SCPI interface to change configuration
        internal ThunderscopeMonitoring Monitoring;         // 16 bytes, Read only from UI perspective, UI optionally displays these values     

        // BridgeConfig is set once from config file or hard coded
        // Hardware, Processing & Monitoring is runtime variable

        internal ThunderscopeMemoryAcquiringRegion AcquiringRegion; // Therefore 'AcquiredRegion' (to be used by UI) is the opposite
    }

    public enum ThunderscopeMemoryAcquiringRegion : byte
    {
        RegionA = 1,
        RegionB = 2
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeBridgeConfig
    {
        // If the contents of this struct changes, consider incrementing the BuildVersion on ThunderscopeBridgeHeader

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