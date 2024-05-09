namespace TS.NET
{
    public class ThunderscopeCalibration
    {
        public ThunderscopeChannelCalibration Channel1 { get; set; }
        public ThunderscopeChannelCalibration Channel2 { get; set; }
        public ThunderscopeChannelCalibration Channel3 { get; set; }
        public ThunderscopeChannelCalibration Channel4 { get; set; }

        public static ThunderscopeCalibration Default()
        {
            return new ThunderscopeCalibration()
            {
                Channel1 = new ThunderscopeChannelCalibration()
                {
                    AttenuatorGain = 0.02,
                    TrimOffsetDac = 2048,
                    TrimSensitivityDac = 64
                },
                Channel2 = new ThunderscopeChannelCalibration()
                {
                    AttenuatorGain = 0.02,
                    TrimOffsetDac = 2048,
                    TrimSensitivityDac = 64
                },
                Channel3 = new ThunderscopeChannelCalibration()
                {
                    AttenuatorGain = 0.02,
                    TrimOffsetDac = 2048,
                    TrimSensitivityDac = 64
                },
                Channel4 = new ThunderscopeChannelCalibration()
                {
                    AttenuatorGain = 0.02,
                    TrimOffsetDac = 2048,
                    TrimSensitivityDac = 64
                }
            };
        }
    }

    // This has the potential to be a complex structure with per-PGA-gain-setting gain & offset, etc. Bodge for now.
    public class ThunderscopeChannelCalibration
    {
        public double AttenuatorGain { get; set; }
        public ushort TrimOffsetDac { get; set; }
        public ushort TrimSensitivityDac { get; set; }
    }
}
