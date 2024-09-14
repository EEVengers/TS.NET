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

        internal ThunderscopeMemoryAcquiringDataRegion AcquiringDataRegion; // Therefore 'AcquiredDataRegion' (to be used by UI) is the opposite
    }

    public enum ThunderscopeMemoryAcquiringDataRegion : byte
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
        public byte MaxDataRegionDataByteWidth;
        public byte DataRegionCount;

        public readonly ulong MaxDataRegionLengthBytes()
        {
            unsafe
            {
                uint headerLength = (uint)sizeof(ThunderscopeBridgeDataRegionHeader);
                return headerLength + (MaxChannelCount * MaxChannelDataLength * MaxDataRegionDataByteWidth);
            }
            
        }

        public readonly ulong MaxAllDataRegionLengthBytes()
        {
            return MaxDataRegionLengthBytes() * DataRegionCount;
        }
    }
}