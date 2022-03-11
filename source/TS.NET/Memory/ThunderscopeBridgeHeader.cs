using System.Runtime.InteropServices;

namespace TS.NET
{


    // Ensure this is blitable (i.e. don't use bool)
    // Pack of 1 = No padding.
    // There might be some benefit later to setting a fixed size (64?) for memory alignment if an aligned memorymappedfile can be created.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]      
    internal struct ThunderscopeBridgeHeader
    {
        // Version + Capacity is enough data for the UI to know how big a memorymappedfile to open
        internal byte Version = 1;          // Allows UI to know which ThunderscopeMemoryBridgeHeader version to use, hence the size of the header.
        internal ulong DataCapacity;        // Maximum size of the data array in bridge. Example: 400M, set from configuration file?

        internal ThunderscopeMemoryBridgeState State;
        internal ThunderscopeConfiguration Configuration;
        internal ThunderscopeMonitoring Monitoring;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeConfiguration
    {
        public Channels Channels;
        public ulong ChannelLength;         // Example: 4 channels with max length = 100M, can easily be 1k for high update rate. Max: Capacity/4, Min: 1k.
        public BoxcarLength BoxcarLength;
        public TriggerChannel TriggerChannel;
        public TriggerMode TriggerMode;
        public ThunderscopeChannelDataType ChannelDataType;
    }

    // Monitoring variables that reset when configuration variables change
    public struct ThunderscopeMonitoring
    {       
        public ulong TotalTriggers;         // All triggers
        public ulong MissedTriggers;        // Triggers that weren't displayed
    }

    // Avoid using 0 value - makes it easier to find bugs [incorrect ser/deser and unset variables]
    public enum ThunderscopeMemoryBridgeState : byte
    {
        Empty = 1,      // Writing is allowed
        Full = 2,       // Writing is blocked, waiting for reader to set back to Unset
    }

    public enum ThunderscopeChannelDataType : byte
    {
        Byte = 1,
        Int16 = 2,
        Int32 = 3
    }
}