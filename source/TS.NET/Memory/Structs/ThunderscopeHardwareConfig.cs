using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeHardwareConfig             // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        // If the contents of this struct changes, consider incrementing the BuildVersion on ThunderscopeBridgeHeader
        public ThunderscopeAcquisitionConfig Acquisition;
        public ThunderscopeChannelFrontendArray Frontend;
        public ThunderscopeExtSyncMode ExtSyncMode;
        public ThunderscopeRefClockMode RefClockMode;
        public uint RefClockFrequencyHz;
    }

    public struct ThunderscopeAcquisitionConfig
    {
        public AdcChannelMode AdcChannelMode;
        public byte EnabledChannels;                    // LSB = Ch0, LSB+1 = Ch1, etc
        public ulong SampleRateHz;
        public AdcResolution Resolution;
    }

    // https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#inline-arrays
    [System.Runtime.CompilerServices.InlineArray(4)]
    public struct ThunderscopeChannelFrontendArray
    {
        ThunderscopeChannelFrontend Frontend;
    }

    public enum AdcChannelMode : byte
    {
        Single = 1,
        Dual = 2,
        Quad = 4
    }

    public enum AdcResolution : byte
    {
        EightBit = 1,
        TwelveBit = 2
    }
}
