using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeChannelCalibration
    {
        public double AttenuatorScale { get; set; }
        public required ThunderscopeChannelPathCalibration[] Paths { get; set; }
        public required Dictionary<uint, ThunderscopePgaLoadScale> PgaLoadScales { get; set; }

        // XDMA calibration
        //public double AttenuatorGain1MOhm { get; set; }
        //public double AttenuatorGain50Ohm { get; set; }
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

        // LiteX calibration
        //public double BufferOffset { get; set; }
        //public double BiasVoltage { get; set; }
        //public double TrimResistorOhms { get; set; }
        //public double PgaLowGainError { get; set; }
        //public double PgaHighGainError { get; set; }
        //public double PgaLowOffsetVoltage { get; set; }
        //public double PgaHighOffsetVoltage { get; set; }
        //public double PgaOutputGainError { get; set; }
        //public double PgaInputBiasCurrent { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeAdcCalibration
    {
        // HMCAD1520 calibration
        public byte FineGainBranch1 { get; set; }
        public byte FineGainBranch2 { get; set; }
        public byte FineGainBranch3 { get; set; }
        public byte FineGainBranch4 { get; set; }
        public byte FineGainBranch5 { get; set; }
        public byte FineGainBranch6 { get; set; }
        public byte FineGainBranch7 { get; set; }
        public byte FineGainBranch8 { get; set; }
    }
}
