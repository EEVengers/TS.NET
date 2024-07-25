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
        public double AttenuatorGainHighZ { get; set; }
        public double AttenuatorGainFiftyOhm { get; set; }
        public double BufferGain { get; set; }
        public double PgaPreampLowGain { get; set; }
        public double PgaPreampHighGain { get; set; }
        public double PgaAttenuatorGain0 { get; set; }
        public double PgaAttenuatorGain1 { get; set; }
        public double PgaAttenuatorGain2 { get; set; }
        public double PgaAttenuatorGain3 { get; set; }
        public double PgaAttenuatorGain4 { get; set; }
        public double PgaAttenuatorGain5 { get; set; }
        public double PgaAttenuatorGain6 { get; set; }
        public double PgaAttenuatorGain7 { get; set; }
        public double PgaAttenuatorGain8 { get; set; }
        public double PgaAttenuatorGain9 { get; set; }
        public double PgaAttenuatorGain10 { get; set; }
        public double PgaOutputAmpGain { get; set; }
        public double HardwareOffsetVoltage { get; set; }
    }
}
