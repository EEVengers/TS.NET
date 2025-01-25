using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET.Driver.LiteX
{
    internal unsafe class LitePcieFile
    {
        private const string USER_DEVICE_PATH = "user";
        private const int FILE_BEGIN = 0;
        private static nint NULL = nint.Zero;
        public const uint GENERIC_READ = (0x80000000);
        public const uint GENERIC_WRITE = (0x40000000);
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        public const uint OPEN_EXISTING = 3;

        private nint fileHandle;

        public unsafe void Open(string devicePath)
        {
            if (OperatingSystem.IsWindows())
            {
                fileHandle = Windows.Interop.CreateFile($"{devicePath}\\CTRL", FileAccess.ReadWrite, FileShare.None, NULL, FileMode.Open, FileAttributes.Normal, NULL);                
                if ((nuint)fileHandle == nuint.MaxValue)
                {
                    throw new ThunderscopeException($"CreateFile - failed ({Marshal.GetLastSystemError()})");                   
                }
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
                            throw new ThunderscopeException($"DeviceIoControl - failed ({Marshal.GetLastSystemError()})");
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
                Windows.Interop.CloseHandle(fileHandle);
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
