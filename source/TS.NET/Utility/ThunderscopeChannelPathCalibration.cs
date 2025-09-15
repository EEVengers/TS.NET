namespace TS.NET
{
    public enum PgaPreampGain
    {
        Low = 0,
        High = 1
    }

    public class ThunderscopeChannelPathCalibration     // LMH6518
    {
        public PgaPreampGain PgaPreampGain { get; set; }
        public byte PgaLadderAttenuator { get; set; }   // One of 11 values: 0 - 10
        public byte TrimScaleDac { get; set; }          // 8 bit raw value
        public double TrimOffsetDacScale { get; set; }  // DAC LSB normalised to ADC fullscale
        public ushort TrimOffsetDacZero { get; set; }   // 12 bit raw value to get ADC midscale
        public double BufferInputVpp { get; set; }      // PGA input volts peak-peak for full ADC range in single channel mode at maximum sample rate
    }
}
