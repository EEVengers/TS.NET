using System.Runtime.InteropServices;

namespace TS.NET
{
    // Ensure this is blitable (i.e. don't use bool)
    // Pack of 1 = No padding.
    // There might be some benefit later to setting a fixed size (64?) for memory alignment if an aligned memorymappedfile can be created.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct ThunderscopeBridgeHeader
    {
        // Version + DataCapacity is enough data for the UI to know how big a memorymappedfile to open
        internal byte Version;              // Allows UI to know which ThunderscopeMemoryBridgeHeader version to use, hence the size of the header.
        internal ulong DataCapacityBytes;   // Maximum size of the data array in bridge.

        internal ushort MaxChannelCount;    
        internal ulong MaxChannelDataLength;        // Could be uint, but 4.3 billion samples (uint.max) is within the capability of a large RAM system so err on the side of caution.
        internal byte MaxChannelDataByteCount;
        // All the above variables can only be set once, during bridge creation.
        // DataCapacityBytes = MaxChannelCount * MaxChannelDataLength * MaxChannelDataByteWidth * 2
        // (* 2 as there are 2 regions used in tick-tock fashion)
        // Multiply by 2 because there is an "AcquiringRegion" and an "AcquiredRegion" so the engine can be populating a region whilst the UI is using the other region. Standard memory swapping.
        //
        // Example memory mapped file layout:
        // [Header][Channel 0 region A][Channel 1 region A][Channel 2 region A][Channel 3 region A][Channel 0 region B][Channel 1 region B][Channel 2 region B][Channel 3 region B]
        // If CurrentChannelBytes is less than MaxChannelBytes then the channel regions pack together without gaps, leaving spare bytes at the very end e.g. [Header][Chan 0...][Chan 1...][Chan 2...][Chan 3...][Chan 0...][Chan 1...][Chan 2...][Chan 3...][Empty bytes]
        // Similar logic if CurrentChannelCount is less than MaxChannelCount
        // This ensures minimum paging

        internal ThunderscopeMemoryAcquiringRegion AcquiringRegion;     // Therefore 'AcquiredRegion' (to be used by UI) is the opposite
        internal ThunderscopeConfiguration Configuration;             // Read only from UI perspective, UI uses SCPI interface to change configuration
        internal ThunderscopeProcessing Processing;         // Read only from UI perspective, UI uses SCPI interface to change configuration
        internal ThunderscopeMonitoring Monitoring;         // Read only from UI perspective, UI optionally displays these values
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeConfiguration             // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        public AdcChannelMode AdcChannelMode;           // The number of channels enabled on ADC. ADC has input mux, e.g. Channel1.Enabled and Channel4.Enabled could have AdcChannels of Two. Useful for UI to know this, in order to clamp maximum sample rate.
        // This is a bit different to "CurrentChannelCount" because it reflects the configuration of the hardware, not the configuration of the memory mapped file. For example: ADC could stream 4 channels, but UI only has 3 enabled.

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        //public ThunderscopeChannel* Channels;         // Commented out as requires unsafe context but maybe switch to it later?
        public ThunderscopeChannel Channel1;
        public ThunderscopeChannel Channel2;
        public ThunderscopeChannel Channel3;
        public ThunderscopeChannel Channel4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeProcessing   // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        public ushort CurrentChannelCount;          // From 1 to ThunderscopeBridgeHeader.MaxChannelCount
        public ulong CurrentChannelDataLength;      // From 1 to ThunderscopeBridgeHeader.MaxChannelDataLength
        public byte CurrentChannelDataByteCount;    // From 1 to ThunderscopeBridgeHeader.MaxChannelDataByteCount
        public HorizontalSumLength HorizontalSumLength;
        public TriggerChannel TriggerChannel;
        public TriggerMode TriggerMode;
        public ThunderscopeChannelDataType ChannelDataType;
    }

    // Monitoring variables that reset when configuration variables change
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeMonitoring
    {
        public ulong TotalAcquisitions;         // All triggers
        public ulong MissedAcquisitions;        // Triggers that weren't displayed
    }

    public enum ThunderscopeMemoryAcquiringRegion : byte
    {
        RegionA = 1,
        RegionB = 2
    }
}