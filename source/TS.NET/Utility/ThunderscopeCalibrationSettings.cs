using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS.NET
{
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(ThunderscopeCalibrationSettings))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }

    public class ThunderscopeCalibrationSettings
    {
        public ThunderscopeChannelCalibrationSettings Channel1 { get; set; } = ThunderscopeChannelCalibrationSettings.Default();
        public ThunderscopeChannelCalibrationSettings Channel2 { get; set; } = ThunderscopeChannelCalibrationSettings.Default();
        public ThunderscopeChannelCalibrationSettings Channel3 { get; set; } = ThunderscopeChannelCalibrationSettings.Default();
        public ThunderscopeChannelCalibrationSettings Channel4 { get; set; } = ThunderscopeChannelCalibrationSettings.Default();
        public ThunderscopeAdcCalibrationSettings Adc { get; set; } = ThunderscopeAdcCalibrationSettings.Default();

        public static ThunderscopeCalibrationSettings Default()
        {
            return new ThunderscopeCalibrationSettings();
        }

        public static ThunderscopeCalibrationSettings FromJsonFile(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(file);

            return JsonSerializer.Deserialize(File.ReadAllText(file), SourceGenerationContext.Default.ThunderscopeCalibrationSettings) ?? throw new ArgumentNullException();
        }

        public void ToJsonFile(string path)
        {
            var json = JsonSerializer.Serialize(this, SourceGenerationContext.Default.ThunderscopeCalibrationSettings);
            File.WriteAllText(path, json);
        }
    }

    public class ThunderscopePgaLoadScale
    {
        public uint SampleRate { get; set; }
        public byte ChannelCount { get; set; }
        public double Scale { get; set; }
    }

    public class ThunderscopeChannelCalibrationSettings
    {
        public double AttenuatorScale { get; set; }
        public required ThunderscopeChannelPathCalibration[] Paths { get; set; }
        public required ThunderscopePgaLoadScale[] PgaLoadScales { get; set; }

        //public double BufferGain { get; set; }
        //public double PgaPreampLowGain { get; set; }
        //public double PgaPreampHighGain { get; set; }
        //public double PgaAttenuatorGain0 { get; set; }
        //public double PgaAttenuatorGain1 { get; set; }
        //public double PgaAttenuatorGain2 { get; set; }
        //public double PgaAttenuatorGain3 { get; set; }
        //public double PgaAttenuatorGain4 { get; set; }
        //public double PgaAttenuatorGain5 { get; set; }
        //public double PgaAttenuatorGain6 { get; set; }
        //public double PgaAttenuatorGain7 { get; set; }
        //public double PgaAttenuatorGain8 { get; set; }
        //public double PgaAttenuatorGain9 { get; set; }
        //public double PgaAttenuatorGain10 { get; set; }
        //public double PgaOutputAmpGain { get; set; }
        //public double HardwareOffsetVoltageLowGain { get; set; }
        //public double HardwareOffsetVoltageHighGain { get; set; }
        //public double BufferOffset { get; set; }
        //public double BiasVoltage { get; set; }
        //public double TrimResistorOhms { get; set; }
        //public double PgaLowGainError { get; set; }
        //public double PgaHighGainError { get; set; }
        //public double PgaLowOffsetVoltage { get; set; }
        //public double PgaHighOffsetVoltage { get; set; }
        //public double PgaOutputGainError { get; set; }
        //public double PgaInputBiasCurrent { get; set; }

        public ThunderscopeChannelCalibration ToDriver()
        {
            return new ThunderscopeChannelCalibration()
            {
                AttenuatorScale = AttenuatorScale,
                Paths = Paths,
                PgaLoadScales = PgaLoadScales
                
                //BufferGain = this.BufferGain,
                //PgaPreampLowGain = this.PgaPreampLowGain,
                //PgaPreampHighGain = this.PgaPreampHighGain,
                //PgaAttenuatorGain0 = this.PgaAttenuatorGain0,
                //PgaAttenuatorGain1 = this.PgaAttenuatorGain1,
                //PgaAttenuatorGain2 = this.PgaAttenuatorGain2,
                //PgaAttenuatorGain3 = this.PgaAttenuatorGain3,
                //PgaAttenuatorGain4 = this.PgaAttenuatorGain4,
                //PgaAttenuatorGain5 = this.PgaAttenuatorGain5,
                //PgaAttenuatorGain6 = this.PgaAttenuatorGain6,
                //PgaAttenuatorGain7 = this.PgaAttenuatorGain7,
                //PgaAttenuatorGain8 = this.PgaAttenuatorGain8,
                //PgaAttenuatorGain9 = this.PgaAttenuatorGain9,
                //PgaAttenuatorGain10 = this.PgaAttenuatorGain10,
                //PgaOutputAmpGain = this.PgaOutputAmpGain,
                //HardwareOffsetVoltageLowGain = this.HardwareOffsetVoltageLowGain,
                //HardwareOffsetVoltageHighGain = this.HardwareOffsetVoltageHighGain,
                //BufferOffset = this.BufferOffset,
                //BiasVoltage = this.BiasVoltage,
                //TrimResistorOhms = this.TrimResistorOhms,
                //PgaLowGainError = this.PgaLowGainError,
                //PgaHighGainError = this.PgaHighGainError,
                //PgaLowOffsetVoltage = this.PgaLowOffsetVoltage,
                //PgaHighOffsetVoltage = this.PgaHighOffsetVoltage,
                //PgaOutputGainError = this.PgaOutputGainError,
                //PgaInputBiasCurrent = this.PgaInputBiasCurrent
            };
        }

        public static ThunderscopeChannelCalibrationSettings Default()
        {
            return new ThunderscopeChannelCalibrationSettings()
            {
                AttenuatorScale = 0.02,
                Paths =
                [
                    new(){ PgaLadderAttenuator = 0, BufferInputVpp = 0.009, TrimScaleDac = 83, TrimOffsetDacScale = 0.5, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 1, BufferInputVpp = 0.012, TrimScaleDac = 66, TrimOffsetDacScale = 0.5, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 2, BufferInputVpp = 0.016, TrimScaleDac = 53, TrimOffsetDacScale = 0.5, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 3, BufferInputVpp = 0.020, TrimScaleDac = 85, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 4, BufferInputVpp = 0.025, TrimScaleDac = 67, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 5, BufferInputVpp = 0.032, TrimScaleDac = 53, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 6, BufferInputVpp = 0.040, TrimScaleDac = 42, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 7, BufferInputVpp = 0.050, TrimScaleDac = 33, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 8, BufferInputVpp = 0.062, TrimScaleDac = 26, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 9, BufferInputVpp = 0.078, TrimScaleDac = 20, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 10, BufferInputVpp = 0.098, TrimScaleDac = 16, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },

                    new(){ PgaLadderAttenuator = 0, BufferInputVpp = 0.098, TrimScaleDac = 16, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 1, BufferInputVpp = 0.124, TrimScaleDac = 12, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 2, BufferInputVpp = 0.156, TrimScaleDac = 10, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 3, BufferInputVpp = 0.196, TrimScaleDac = 8, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 4, BufferInputVpp = 0.247, TrimScaleDac = 6, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 5, BufferInputVpp = 0.310, TrimScaleDac = 4, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 6, BufferInputVpp = 0.390, TrimScaleDac = 3, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 7, BufferInputVpp = 0.491, TrimScaleDac = 2, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 8, BufferInputVpp = 0.618, TrimScaleDac = 2, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 9, BufferInputVpp = 0.777, TrimScaleDac = 1, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                    new(){ PgaLadderAttenuator = 10, BufferInputVpp = 0.977, TrimScaleDac = 1, TrimOffsetDacScale = 0.25, TrimOffsetDacZero = 2048 },
                ],
                PgaLoadScales = []
                //BufferGain = 0,
                //PgaPreampLowGain = 10,
                //PgaPreampHighGain = 30,
                //PgaAttenuatorGain0 = 0,
                //PgaAttenuatorGain1 = -2,
                //PgaAttenuatorGain2 = -4,
                //PgaAttenuatorGain3 = -6,
                //PgaAttenuatorGain4 = -8,
                //PgaAttenuatorGain5 = -10,
                //PgaAttenuatorGain6 = -12,
                //PgaAttenuatorGain7 = -14,
                //PgaAttenuatorGain8 = -16,
                //PgaAttenuatorGain9 = -18,
                //PgaAttenuatorGain10 = -20,
                //PgaOutputAmpGain = 8.86,
                //HardwareOffsetVoltageLowGain = 2.525,
                //HardwareOffsetVoltageHighGain = 2.525,
                //BufferOffset = 2.5,
                //BiasVoltage = 2.5,
                //TrimResistorOhms = 50000,
                //PgaLowGainError = 0,
                //PgaHighGainError = 0,
                //PgaLowOffsetVoltage = 0,
                //PgaHighOffsetVoltage = 0,
                //PgaOutputGainError = 0,
                //PgaInputBiasCurrent = 40
            };
        }
    }

    // ADC Calibration Data
    public class ThunderscopeAdcCalibrationSettings
    {
        public byte FineGainBranch1 { get; set; }
        public byte FineGainBranch2 { get; set; }
        public byte FineGainBranch3 { get; set; }
        public byte FineGainBranch4 { get; set; }
        public byte FineGainBranch5 { get; set; }
        public byte FineGainBranch6 { get; set; }
        public byte FineGainBranch7 { get; set; }
        public byte FineGainBranch8 { get; set; }

        public ThunderscopeAdcCalibration ToDriver()
        {
            return new ThunderscopeAdcCalibration()
            {
                FineGainBranch1 = this.FineGainBranch1,
                FineGainBranch2 = this.FineGainBranch2,
                FineGainBranch3 = this.FineGainBranch3,
                FineGainBranch4 = this.FineGainBranch4,
                FineGainBranch5 = this.FineGainBranch5,
                FineGainBranch6 = this.FineGainBranch6,
                FineGainBranch7 = this.FineGainBranch7,
                FineGainBranch8 = this.FineGainBranch8
            };
        }

        public static ThunderscopeAdcCalibrationSettings Default()
        {
            return new ThunderscopeAdcCalibrationSettings()
            {
                FineGainBranch1 = 0,
                FineGainBranch2 = 0,
                FineGainBranch3 = 0,
                FineGainBranch4 = 0,
                FineGainBranch5 = 0,
                FineGainBranch6 = 0,
                FineGainBranch7 = 0,
                FineGainBranch8 = 0
            };
        }
    }
}

