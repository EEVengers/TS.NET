using System.Runtime.InteropServices;

namespace TS.NET.Driver.LiteX
{
    enum LitePcieIoctl
    {
        Register = 0,
        Flash = 1,
        Icap = 2        // Xilinx Internal Configuration Access Port
    }

    [StructLayout(LayoutKind.Sequential)]
    record struct LitepcieIoctlRegister(uint Address, uint Value, byte IsWrite);

    [StructLayout(LayoutKind.Sequential)]
    record struct LitepcieIoctlIcap(byte Address, uint Data);

    public class LitePcie
    {
        private LitePcieFile? file;
        private bool opened = false;

        public void Open(string devicePath)
        {
            file = new LitePcieFile();
            file.Open(devicePath);
            opened = true;
        }

        public static int CTL_CODE(int DeviceType, int Function, int Method, int Access) => (DeviceType << 16) | (Access << 14) | (Function << 2) | Method;
        private const int FILE_DEVICE_UNKNOWN = 0x00000022;
        private const int METHOD_BUFFERED = 0;
        private const int FILE_ANY_ACCESS = 0;

        public uint ReadL(uint address)
        {
            if (!opened)
                throw new ThunderscopeException("LitePcie not opened");
            var data = new LitepcieIoctlRegister() { Address = address, IsWrite = 0 };
            file?.IoControl((uint)CTL_CODE(FILE_DEVICE_UNKNOWN, (int)LitePcieIoctl.Register, METHOD_BUFFERED, FILE_ANY_ACCESS), MemoryMarshal.AsBytes(new Span<LitepcieIoctlRegister>(ref data)));
            return data.Value;
        }

        public void WriteL(uint address, uint value)
        {
            if (!opened)
                throw new ThunderscopeException("LitePcie not opened");
            var data = new LitepcieIoctlRegister() { Address = address, Value = value, IsWrite = 1 };
            file?.IoControl((uint)CTL_CODE(FILE_DEVICE_UNKNOWN, (int)LitePcieIoctl.Register, METHOD_BUFFERED, FILE_ANY_ACCESS), MemoryMarshal.AsBytes(new Span<LitepcieIoctlRegister>(ref data)));
        }

        public void Reload()
        {
            if (!opened)
                throw new ThunderscopeException("LitePcie not opened");
            var data = new LitepcieIoctlIcap() { Address = 0x4, Data = 0xF };
            file?.IoControl((uint)CTL_CODE(FILE_DEVICE_UNKNOWN, (int)LitePcieIoctl.Icap, METHOD_BUFFERED, FILE_ANY_ACCESS), MemoryMarshal.AsBytes(new Span<LitepcieIoctlIcap>(ref data)));
        }

        public void Close()
        {
            if (!opened)
                throw new ThunderscopeException("LitePcie not opened");
            file?.Close();
            file = null;
        }
    }
}
