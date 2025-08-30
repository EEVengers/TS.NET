using System.Runtime.InteropServices;

namespace TS.NET
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThunderscopeChannelFrontendManualControl
    {
        public ThunderscopeCoupling Coupling;
        public ThunderscopeTermination Termination;

        // Work in progress
        public byte Attenuator;
        public ushort DAC;
        public byte DPOT;
        
        public byte PgaLadderAttenuation;
        public ThunderscopeBandwidth PgaFilter;
        public byte PgaHighGain;
    }
}
