using System.Runtime.InteropServices;
using System.Numerics;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeHardwareConfig             // Idempotent so that UI doesn't have to store state and removes the possibility of config mismatch with multiple actors changing config (e.g. SCPI and Web UI)
    {
        // If the contents of this struct changes, consider incrementing the BuildVersion on ThunderscopeBridgeHeader

        public AdcChannelMode AdcChannelMode;           // The number of channels enabled on ADC. ADC has input mux, e.g. Channel1.Enabled and Channel4.Enabled could have AdcChannels of Two. Useful for UI to know this, in order to clamp maximum sample rate.
        public ulong SampleRateHz;                      // Channel sample rate (not ADC sample rate)
        public byte EnabledChannels;                    // LSB = Ch0, LSB+1 = Ch1, etc

        public ThunderscopeChannelFrontendArray Frontend;
        public ThunderscopeChannelCalibrationArray Calibration;

        public ushort EnabledChannelsCount()
        {
            return EnabledChannels switch
            {
                0 => 0,
                1 => 1,
                2 => 1,
                3 => 2,
                4 => 1,
                5 => 2,
                6 => 2,
                7 => 3,
                8 => 1,
                9 => 2,
                10 => 2,
                11 => 3,
                12 => 2,
                13 => 3,
                14 => 3,
                15 => 4,
                _ => throw new NotImplementedException()
            };
        }
    }

    // https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12#inline-arrays
    [System.Runtime.CompilerServices.InlineArray(4)]
    public struct ThunderscopeChannelFrontendArray
    {
        ThunderscopeChannelFrontend Frontend;
    }

    [System.Runtime.CompilerServices.InlineArray(4)]
    public struct ThunderscopeChannelCalibrationArray
    {
        ThunderscopeChannelCalibration Calibration;
    }

    public enum AdcChannelMode : byte
    {
        Single = 1,
        Dual = 2,
        Quad = 4
    }
}
