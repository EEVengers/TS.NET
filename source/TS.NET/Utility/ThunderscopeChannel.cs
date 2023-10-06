using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]     // Struct packing allows the use of this datatype in ThunderscopeBridge header as it's consistent with the other datatypes
    public struct ThunderscopeChannel
    {
        public bool Enabled;
        public ThunderscopeCoupling Coupling;
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
                Enabled = true,
                Coupling = ThunderscopeCoupling.DC,
                VoltFullScale = 0.5,
                VoltOffset = 0,
                Bandwidth = 350,               
            };
        }
    }

    public enum ThunderscopeCoupling
    {
        DC = 1,
        AC = 2
    }
}
