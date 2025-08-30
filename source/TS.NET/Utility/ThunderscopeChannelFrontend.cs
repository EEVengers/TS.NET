using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]     // Struct packing allows the use of this datatype in ThunderscopeBridge header as it's consistent with the other datatypes
    public struct ThunderscopeChannelFrontend
    {
        public ThunderscopeCoupling Coupling;
        public ThunderscopeTermination Termination;
        public double RequestedVoltFullScale;
        public double RequestedVoltOffset;
        public ThunderscopeBandwidth Bandwidth;

        // Used by calibration for fixed setting of PGA gain & attenuator
        public bool PgaConfigWordOverride;

        // Calculated data
        //public bool Attenuator1MOhm;
        //public bool Attenuator50Ohm;
        public ushort PgaConfigWord;
        //public double ActualSystemGain;
        public double ActualVoltFullScale;
        public double ActualVoltOffset;
        //public ushort TrimOffsetDac;
        //public ushort TrimSensitivityDac;

        public static ThunderscopeChannelFrontend Default()
        {
            return new ThunderscopeChannelFrontend()
            {
                Coupling = ThunderscopeCoupling.DC,
                Termination = ThunderscopeTermination.OneMegaohm,
                RequestedVoltFullScale = 0.8,
                RequestedVoltOffset = 0,
                Bandwidth = ThunderscopeBandwidth.BwFull
            };
        }

        /// <param name="ladderAttenuation">0 to 10, corresponding to 0dB to -20dB in 2dB steps</param>
        /// <param name="preampHighGain">+30dB if true, +10dB if false</param>
        public void CalculatePgaConfigWord(byte ladderAttenuation, bool preampHighGain, ThunderscopeBandwidth filter)
        {
            if (ladderAttenuation > 10)
                ladderAttenuation = 10;
            PgaConfigWord = ladderAttenuation;
            if (preampHighGain)
                PgaConfigWord |= 0x10;
            // Always set the Aux Hi-Z
            PgaConfigWord |= 0x400;
            PgaConfigWord |= filter switch
            {
                ThunderscopeBandwidth.BwFull => 0,
                ThunderscopeBandwidth.Bw20M => 1 << 6,
                ThunderscopeBandwidth.Bw100M => 2 << 6,
                ThunderscopeBandwidth.Bw200M => 3 << 6,
                ThunderscopeBandwidth.Bw350M => 4 << 6,
                ThunderscopeBandwidth.Bw650M => 5 << 6,
                ThunderscopeBandwidth.Bw750M => 6 << 6,
                _ => throw new Exception("ThunderscopeBandwidth enum value not handled")
            };
        }

        public bool PgaAux() => (PgaConfigWord & 0x400) > 0;
        public ThunderscopeBandwidth PgaFilter() => (ThunderscopeBandwidth)((PgaConfigWord & 0x1C0) >> 6);
        public bool PgaHighGain() => (PgaConfigWord & 0x10) > 0;
        public int PgaAttenuator() => PgaConfigWord & 0x0F;
        public string PgaToString() => $"aux {(PgaAux() ? "hi-z" : "on")}, filter {PgaFilter()}, preamp {(PgaHighGain() ? "HG" : "LG")}, ladder {PgaAttenuator()}";
    }

    public enum ThunderscopeCoupling : byte
    {
        DC = 1,
        AC = 2
    }

    public enum ThunderscopeTermination : byte
    {
        OneMegaohm = 1,
        FiftyOhm = 2
    }

    public enum ThunderscopeBandwidth : byte
    {
        BwFull = 0,
        Bw20M = 1,
        Bw100M = 2,
        Bw200M = 3,
        Bw350M = 4,
        Bw650M = 5,
        Bw750M = 6
    }
}
