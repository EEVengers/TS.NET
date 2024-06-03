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
        public int Bandwidth;

        // Calculated data
        public bool Attenuator;
        public double ActualVoltFullScale;
        public byte PgaConfigurationByte;

        public static ThunderscopeChannel Default()
        {
            return new ThunderscopeChannel()
            {
                Coupling = ThunderscopeCoupling.DC,
                VoltFullScale = 0.5,
                VoltOffset = 0,
                Bandwidth = 20,               
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
}
