using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace TS.NET.JTAG.D2xx;

internal static partial class Interop
{
    private const string LibraryName = "ftd2xx";
    private static readonly string[] CandidateLibraryNames = GetCandidateLibraryNames();

    static Interop()
    {
        NativeLibrary.SetDllImportResolver(typeof(Interop).Assembly, ResolveLibrary);
    }

    internal enum FtStatus : uint
    {
        Ok = 0,
        InvalidHandle = 1,
        DeviceNotFound = 2,
        DeviceNotOpened = 3,
        IoError = 4,
        InsufficientResources = 5,
        InvalidParameter = 6,
        InvalidBaudRate = 7,
        DeviceNotOpenedForErase = 8,
        DeviceNotOpenedForWrite = 9,
        FailedToWriteDevice = 10,
        EepromReadFailed = 11,
        EepromWriteFailed = 12,
        EepromEraseFailed = 13,
        EepromNotPresent = 14,
        EepromNotProgrammed = 15,
        InvalidArgs = 16,
        NotSupported = 17,
        OtherError = 18,
    }

    [Flags]
    internal enum PurgeMask : uint
    {
        Rx = 1,
        Tx = 2,
    }

    [LibraryImport(LibraryName, EntryPoint = "FT_CreateDeviceInfoList")]
    internal static partial FtStatus CreateDeviceInfoList(ref uint numDevices);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8, EntryPoint = "FT_GetDeviceInfoDetail")]
    internal static unsafe partial FtStatus GetDeviceInfoDetail(uint index, ref uint flags, ref uint type, ref uint id, ref uint locationId, byte* serialNumber, byte* description, ref nint ftHandle);

    [LibraryImport(LibraryName, EntryPoint = "FT_Open")]
    internal static partial FtStatus Open(int deviceNumber, out nint handle);

    [LibraryImport(LibraryName, EntryPoint = "FT_Close")]
    internal static partial FtStatus Close(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "FT_ResetDevice")]
    internal static partial FtStatus ResetDevice(nint handle);

    [LibraryImport(LibraryName, EntryPoint = "FT_Purge")]
    internal static partial FtStatus Purge(nint handle, PurgeMask mask);

    [LibraryImport(LibraryName, EntryPoint = "FT_SetTimeouts")]
    internal static partial FtStatus SetTimeouts(nint handle, uint readTimeoutMs, uint writeTimeoutMs);

    [LibraryImport(LibraryName, EntryPoint = "FT_SetLatencyTimer")]
    internal static partial FtStatus SetLatencyTimer(nint handle, byte latency);

    [LibraryImport(LibraryName, EntryPoint = "FT_SetBitMode")]
    internal static partial FtStatus SetBitMode(nint handle, byte mask, byte mode);

    [LibraryImport(LibraryName, EntryPoint = "FT_Read")]
    internal static partial FtStatus Read(nint handle, [Out] byte[] buffer, uint bytesToRead, ref uint bytesReturned);

    [LibraryImport(LibraryName, EntryPoint = "FT_Write")]
    internal static partial FtStatus Write(nint handle, byte[] buffer, uint bytesToWrite, ref uint bytesWritten);

    internal static void EnsureAvailable()
    {
        if (!TryLoadLibrary(out nint handle))
        {
            string os = RuntimeInformation.OSDescription.Trim();
            string arch = RuntimeInformation.ProcessArchitecture.ToString();
            string triedNames = string.Join(", ", CandidateLibraryNames);
            throw new D2xxException($"Unable to load the FTDI D2XX native library on {os} ({arch}). Tried: {triedNames}.");
        }

        NativeLibrary.Free(handle);
    }

    internal static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> buffer)
    {
        int zeroIndex = buffer.IndexOf((byte)0);
        ReadOnlySpan<byte> text = zeroIndex >= 0 ? buffer[..zeroIndex] : buffer;
        return Encoding.UTF8.GetString(text);
    }

    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return nint.Zero;
        }

        return TryLoadLibrary(out nint handle, assembly, searchPath) ? handle : nint.Zero;
    }

    private static bool TryLoadLibrary(out nint handle, Assembly? assembly = null, DllImportSearchPath? searchPath = null)
    {
        foreach (string candidate in CandidateLibraryNames)
        {
            if (assembly is not null && NativeLibrary.TryLoad(candidate, assembly, searchPath, out handle))
            {
                return true;
            }

            if (NativeLibrary.TryLoad(candidate, out handle))
            {
                return true;
            }
        }

        handle = nint.Zero;
        return false;
    }

    private static string[] GetCandidateLibraryNames()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["ftd2xx", "ftd2xx.dll"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ["ftd2xx", "libftd2xx.so"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ["ftd2xx", "libftd2xx.dylib"];
        }

        return [LibraryName];
    }

}