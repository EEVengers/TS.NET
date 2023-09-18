using System;
using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]     // Struct packing allows the use of this datatype in ThunderscopeBridge header as it's consistent with the other datatypes
    public struct ThunderscopeChannel
    {
        public bool Enabled;
        public double VoltsOffset;
        public int VoltsDiv;
        public int Bandwidth;
        public ThunderscopeCoupling Coupling;
        public bool Term50Z;

        public ThunderscopeChannel()
        {
            Enabled = true;
            VoltsOffset = 0;
            VoltsDiv = 100;
            Bandwidth = 350;
            Coupling = ThunderscopeCoupling.DC;
            Term50Z = false;
        }
    }

    public enum ThunderscopeCoupling
    {
        DC = 1,
        AC = 2
    }
}
