using System.Runtime.InteropServices;

namespace TS.NET.Driver.LiteX
{
    enum LitePcieIoctl
    {
        Register = 0,
        Flash = 1,
        Icap = 2        // Xilinx Internal Configuration Access Port
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    record struct LitepcieIoctlRegister(uint Address, uint Value, byte IsWrite);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    record struct LitepcieIoctlIcap(byte Address, uint Data);

    internal class LitePcie
    {
        private readonly LitePcieFile file;

        public LitePcie(string devicePath)
        {
            file = new LitePcieFile();
            file.Open(devicePath);
        }

        public uint ReadL(uint address)
        {
            var data = new LitepcieIoctlRegister() { Address = address, IsWrite = 0 };
            file.IoControl((uint)LitePcieIoctl.Register, MemoryMarshal.Cast<LitepcieIoctlRegister, byte>(new Span<LitepcieIoctlRegister>(ref data)));
            return data.Value;
        }

        public void WriteL(uint address, uint value)
        {
            var data = new LitepcieIoctlRegister() { Address = address, Value = value, IsWrite = 1 };
            file.IoControl((uint)LitePcieIoctl.Register, MemoryMarshal.Cast<LitepcieIoctlRegister, byte>(new Span<LitepcieIoctlRegister>(ref data)));
        }

        public void Reload()
        {
            var data = new LitepcieIoctlIcap() { Address = 0x4, Data = 0xF };
            file.IoControl((uint)LitePcieIoctl.Icap, MemoryMarshal.Cast<LitepcieIoctlIcap, byte>(new Span<LitepcieIoctlIcap>(ref data)));
        }
    }
}
