using YamlDotNet.Serialization;

namespace TS.NET
{
    [YamlSerializable]
    public class ThunderscopeCalibrationSettings
    {
        public ThunderscopeChannelCalibrationSettings Channel1 { get; set; }
        public ThunderscopeChannelCalibrationSettings Channel2 { get; set; }
        public ThunderscopeChannelCalibrationSettings Channel3 { get; set; }
        public ThunderscopeChannelCalibrationSettings Channel4 { get; set; }

        public ThunderscopeCalibration ToDriver()
        {
            return new ThunderscopeCalibration()
            {
                Channel1 = this.Channel1.ToDriver(),
                Channel2 = this.Channel2.ToDriver(),
                Channel3 = this.Channel3.ToDriver(),
                Channel4 = this.Channel4.ToDriver(),
            };
        }

        public static ThunderscopeCalibrationSettings Default()
        {
            return new ThunderscopeCalibrationSettings()
            {
                Channel1 = ThunderscopeChannelCalibrationSettings.Default(),
                Channel2 = ThunderscopeChannelCalibrationSettings.Default(),
                Channel3 = ThunderscopeChannelCalibrationSettings.Default(),
                Channel4 = ThunderscopeChannelCalibrationSettings.Default(),
            };
        }
    }

    // This has the potential to be a complex structure with per-PGA-gain-setting gain & offset, etc. Bodge for now.
    [YamlSerializable]
    public class ThunderscopeChannelCalibrationSettings : ThunderscopeChannelCalibration
    {
        public ThunderscopeChannelCalibration ToDriver()
        {
            return new ThunderscopeChannelCalibration()
            {
                AttenuatorGain = this.AttenuatorGain,
                TrimOffsetDac = this.TrimOffsetDac,
                TrimSensitivityDac = this.TrimSensitivityDac
            };
        }

        public static ThunderscopeChannelCalibrationSettings Default()
        {
            return new ThunderscopeChannelCalibrationSettings()
            {
                AttenuatorGain = 0.02,
                TrimOffsetDac = 2048,
                TrimSensitivityDac = 64
            };
        }
    }
}
