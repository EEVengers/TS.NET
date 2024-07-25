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
                AttenuatorGainHighZ = this.AttenuatorGainHighZ,
                AttenuatorGainFiftyOhm = this.AttenuatorGainFiftyOhm,
                BufferGain = this.BufferGain,
                PgaPreampLowGain = this.PgaPreampLowGain,
                PgaPreampHighGain = this.PgaPreampHighGain,
                PgaAttenuatorGain0 = this.PgaAttenuatorGain0,
                PgaAttenuatorGain1 = this.PgaAttenuatorGain1,
                PgaAttenuatorGain2 = this.PgaAttenuatorGain2,
                PgaAttenuatorGain3 = this.PgaAttenuatorGain3,
                PgaAttenuatorGain4 = this.PgaAttenuatorGain4,
                PgaAttenuatorGain5 = this.PgaAttenuatorGain5,
                PgaAttenuatorGain6 = this.PgaAttenuatorGain6,
                PgaAttenuatorGain7 = this.PgaAttenuatorGain7,
                PgaAttenuatorGain8 = this.PgaAttenuatorGain8,
                PgaAttenuatorGain9 = this.PgaAttenuatorGain9,
                PgaAttenuatorGain10 = this.PgaAttenuatorGain10,
                PgaOutputAmpGain = this.PgaOutputAmpGain,
                HardwareOffsetVoltage = this.HardwareOffsetVoltage
            };
        }

        public static ThunderscopeChannelCalibrationSettings Default()
        {
            return new ThunderscopeChannelCalibrationSettings()
            {
                AttenuatorGainHighZ = -33.9794,
                AttenuatorGainFiftyOhm = -13.9794,
                BufferGain = 0,
                PgaPreampLowGain = 10,
                PgaPreampHighGain = 30,
                PgaAttenuatorGain0 = 0,
                PgaAttenuatorGain1 = -2,
                PgaAttenuatorGain2 = -4,
                PgaAttenuatorGain3 = -6,
                PgaAttenuatorGain4 = -8,
                PgaAttenuatorGain5 = -10,
                PgaAttenuatorGain6 = -12,
                PgaAttenuatorGain7 = -14,
                PgaAttenuatorGain8 = -16,
                PgaAttenuatorGain9 = -18,
                PgaAttenuatorGain10 = -20,
                PgaOutputAmpGain = 8.86,
                HardwareOffsetVoltage = 2.525
            };
        }
    }
}
