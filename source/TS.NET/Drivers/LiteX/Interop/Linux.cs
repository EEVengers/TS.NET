using System.Runtime.InteropServices;

namespace TS.NET.Driver.LiteX.Linux
{
    [Flags]
    internal enum OpenFlags : uint
    {
        O_RDONLY = 0,
        O_WRONLY = 1,
        O_RDWR = 2,
    }

    internal static class Interop
    {
        [DllImport("libc.so.6", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        public static extern Int32 open(
            [MarshalAs(UnmanagedType.LPStr)] string pathname,
            Int32 flags);

        [DllImport("libc.so.6", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        public static unsafe extern Int32 pread(
            nint fildes,
            byte* buf,
            Int32 nbyte,
            Int32 offset);

        [DllImport("libc.so.6", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        public static unsafe extern Int32 pwrite(
            nint fildes,
            byte* buf,
            Int32 nbyte,
            Int32 offset);
    }
}
