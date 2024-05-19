namespace TS.NET
{
    public class ThunderscopeCalibration
    {
        public ThunderscopeChannelCalibration Channel1 { get; set; }
        public ThunderscopeChannelCalibration Channel2 { get; set; }
        public ThunderscopeChannelCalibration Channel3 { get; set; }
        public ThunderscopeChannelCalibration Channel4 { get; set; }
    }

    // This has the potential to be a complex structure with per-PGA-gain-setting gain & offset, etc. Bodge for now.
    public class ThunderscopeChannelCalibration
    {
        public double AttenuatorGain { get; set; }
        public ushort TrimOffsetDac { get; set; }
        public ushort TrimSensitivityDac { get; set; }
    }
}
