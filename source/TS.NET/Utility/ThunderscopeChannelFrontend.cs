using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]     // Struct packing allows the use of this datatype in ThunderscopeBridge header as it's consistent with the other datatypes
    public struct ThunderscopeChannelFrontend
    {
        public ThunderscopeCoupling Coupling;
        public ThunderscopeTermination Termination;
        public double VoltFullScale;
        public double VoltOffset;
        public ThunderscopeBandwidth Bandwidth;

        // Used by calibration for fixed setting of PGA gain & attenuator
        public bool PgaConfigWordOverride;

        // Calculated data
        public ushort PgaConfigWord;
        public bool Attenuator;
        public double ActualVoltFullScale;
        public ushort TrimOffsetDac;
        public ushort TrimSensitivityDac;

        public static ThunderscopeChannelFrontend Default()
        {
            return new ThunderscopeChannelFrontend()
            {
                Coupling = ThunderscopeCoupling.DC,
                VoltFullScale = 0.5,
                VoltOffset = 0,
                Bandwidth = ThunderscopeBandwidth.BwFull               
            };
        }

        public bool PgaHighGain() => (PgaConfigWord & 0x10) > 0;
        public int PgaAttenuator() => (PgaConfigWord & 0x0F);
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
