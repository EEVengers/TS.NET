using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET.Driver.LiteX
{
    internal class LitePcieFile
    {
        private const string USER_DEVICE_PATH = "user";
        private const int FILE_BEGIN = 0;
        private static nint NULL = nint.Zero;

        private nint fileHandle;

        public void Open(string devicePath)
        {
            if (OperatingSystem.IsWindows())
            {
                fileHandle = Windows.Interop.CreateFile($"{devicePath}\\{USER_DEVICE_PATH}", FileAccess.ReadWrite, FileShare.None, NULL, FileMode.Open, FileAttributes.Normal, NULL);
            }
            if (OperatingSystem.IsLinux())
            {
                fileHandle = Linux.Interop.open($"{devicePath}_{USER_DEVICE_PATH}", (Int32)Linux.OpenFlags.O_RDWR);
            }
            if (OperatingSystem.IsMacOS())
            {
                throw new NotImplementedException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Read(ulong address, Span<byte> data)
        {
            if (OperatingSystem.IsWindows())
            {
                if (!Windows.Interop.SetFilePointerEx(fileHandle, address, NULL, FILE_BEGIN))
                    throw new Exception($"SetFilePointerEx - failed ({Marshal.GetLastWin32Error()})");

                unsafe
                {
                    fixed (byte* dataPtr = data)
                    {
                        if (!Windows.Interop.ReadFile(fileHandle, dataPtr, (uint)data.Length, out uint bytesRead, NULL))
                            throw new Exception($"ReadFile - failed ({Marshal.GetLastWin32Error()})");
                        if (bytesRead != data.Length)
                            throw new Exception("ReadFile - failed to read all bytes");
                    }
                }
            }
            if (OperatingSystem.IsLinux())
            {
                unsafe
                {
                    fixed (byte* dataPtr = data)
                    {
                        int bytesRead = Linux.Interop.pread(fileHandle, dataPtr, data.Length, (int)address);
                        if (bytesRead != data.Length)
                            throw new Exception($"pread user - failed -> toRead={data.Length}, read={bytesRead}, errno={Marshal.GetLastWin32Error()}");
                    }
                }
            }
            if (OperatingSystem.IsMacOS())
            {
                throw new NotImplementedException();
            }
        }

        public void Write(ulong address, ReadOnlySpan<byte> data)
        {
            if (OperatingSystem.IsWindows())
            {
                if (!Windows.Interop.SetFilePointerEx(fileHandle, address, NULL, FILE_BEGIN))
                    throw new Exception($"SetFilePointerEx - failed ({Marshal.GetLastWin32Error()})");

                unsafe
                {
                    fixed (byte* dataPtr = data)
                    {
                        if (!Windows.Interop.WriteFile(fileHandle, dataPtr, (uint)data.Length, out uint bytesWritten, NULL))
                            throw new Exception($"WriteFile - failed ({Marshal.GetLastWin32Error()})");
                    }
                }
            }
            if (OperatingSystem.IsLinux())
            {
                unsafe
                {
                    fixed (byte* dataPtr = data)
                    {
                        Int32 bytesWritten = Linux.Interop.pwrite(fileHandle, dataPtr, (Int32)data.Length, (Int32)address);

                        if (bytesWritten != data.Length)
                            throw new Exception($"pwrite user - failed -> toWrite={data.Length}, written={bytesWritten}, errno={Marshal.GetLastWin32Error()}");
                    }
                }
            }
            if (OperatingSystem.IsMacOS())
            {
                throw new NotImplementedException();
            }
        }

        public void IoControl(uint ioctlCode, ReadOnlySpan<byte> data)
        {
            if (OperatingSystem.IsWindows())
            {
                unsafe
                {
                    fixed (byte* dataPtr = data)
                    {
                        uint bytesReturned = 0;
                        bool success = Windows.Interop.DeviceIoControl(
                            fileHandle,
                            ioctlCode,
                            dataPtr,
                            (uint)data.Length,
                            dataPtr,
                            (uint)data.Length,
                            ref bytesReturned,
                            IntPtr.Zero);

                        if (!success)
                        {
                            int errorCode = Marshal.GetLastWin32Error();
                            // Log it...
                        }
                    }
                }
            }
            if (OperatingSystem.IsLinux())
            {
                throw new NotImplementedException();
            }
            if (OperatingSystem.IsMacOS())
            {
                throw new NotImplementedException();
            }
        }

        public void Close()
        {
            if (OperatingSystem.IsWindows())
            {
                throw new NotImplementedException();
            }
            if (OperatingSystem.IsLinux())
            {
                throw new NotImplementedException();
            }
            if (OperatingSystem.IsMacOS())
            {
                throw new NotImplementedException();
            }
        }
    }
}
