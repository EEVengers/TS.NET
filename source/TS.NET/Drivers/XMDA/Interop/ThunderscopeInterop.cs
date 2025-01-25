using System.Runtime.InteropServices;
using TS.NET.Driver.Shared;
using TS.NET.Driver.XMDA.Interop.Linux;
using TS.NET.Driver.XMDA.Interop.Windows;

namespace TS.NET.Driver.XMDA.Interop
{
    public abstract class ThunderscopeInterop : IDisposable
    {
        public static List<ThunderscopeDevice> IterateDevices() {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                ThunderscopeInteropWindows.PlatIterateDevices() : ThunderscopeInteropLinux.PlatIterateDevices();
        }

        public static ThunderscopeInterop CreateInterop(ThunderscopeDevice dev) {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                new ThunderscopeInteropWindows(dev) : new ThunderscopeInteropLinux(dev);
        }

        public abstract void Dispose();

        public abstract void WriteUser(ReadOnlySpan<byte> data, ulong addr);

        public abstract void ReadUser(Span<byte> data, ulong addr);

        public abstract void ReadC2H(ThunderscopeMemory data, ulong offset, ulong addr, ulong length);
    }
}
