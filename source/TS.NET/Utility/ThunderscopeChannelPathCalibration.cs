using System.Runtime.InteropServices;

namespace TS.NET
{
    public enum PgaPreampGain
    {
        Low = 0,
        High = 1
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeChannelPathCalibration     // LMH6518
    {
        public PgaPreampGain PgaPreampGain { get; set; }
        public byte PgaLadderAttenuator { get; set; }   // One of 11 values: 0 - 10
        public byte TrimScaleDac { get; set; }          // 8 bit raw value
        public double TrimOffsetDacScaleV { get; set; } // PGA input volts per DAC LSB
        public ushort TrimOffsetDacZero { get; set; }   // 12 bit raw value to get ADC midscale
        public Dictionary<uint, double> BufferInputVpp { get; set; } // PGA input volts peak-peak for full ADC range and given sample rate
    }
}
