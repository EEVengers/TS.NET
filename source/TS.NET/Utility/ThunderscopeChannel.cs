using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]     // Struct packing allows the use of this datatype in ThunderscopeBridge header as it's consistent with the other datatypes
    public struct ThunderscopeChannel
    {
        public ThunderscopeCoupling Coupling;
        public ThunderscopeTermination Termination;
        public double VoltFullScale;
        public double VoltOffset;
        public ThunderscopeBandwidth Bandwidth;

        // Calculated data
        public bool Attenuator;
        public double ActualVoltFullScale;
        public ushort PgaConfigurationWord;

        public static ThunderscopeChannel Default()
        {
            return new ThunderscopeChannel()
            {
                Coupling = ThunderscopeCoupling.DC,
                VoltFullScale = 0.5,
                VoltOffset = 0,
                Bandwidth = ThunderscopeBandwidth.Bw20M               
            };
        }
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
