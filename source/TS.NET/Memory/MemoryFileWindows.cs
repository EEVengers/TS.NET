// https://github.com/cloudtoid/interprocess
using System.IO.MemoryMappedFiles;

namespace TS.NET.Memory.Windows
{
    internal sealed class MemoryFileWindows : IMemoryFile
    {
        private const string MapNamePrefix = "TS_NET_";

        internal MemoryFileWindows(string memoryName, ulong bridgeCapacityBytes)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException();

            MappedFile = MemoryMappedFile.CreateOrOpen(
                mapName: MapNamePrefix + memoryName,
                (long)bridgeCapacityBytes,
                MemoryMappedFileAccess.ReadWrite,
                MemoryMappedFileOptions.None,
                HandleInheritability.None);
        }

        public MemoryMappedFile MappedFile { get; }

        public void Dispose()
            => MappedFile.Dispose();

        public static bool Exists(string memoryName)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException();

            try
            {
                using var file = MemoryMappedFile.OpenExisting(MapNamePrefix + memoryName); 
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
