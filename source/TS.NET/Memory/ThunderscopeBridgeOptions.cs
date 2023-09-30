using TS.NET.Memory;

namespace TS.NET
{
    public sealed class ThunderscopeBridgeOptions
    {
        public string MemoryName { get; }
        public string Path { get; }
        public ulong BridgeCapacityBytes { get; }
        public ulong DataCapacityBytes { get; }
        public byte MaxChannelCount { get; }
        public ulong MaxChannelBytes { get; }

        public ThunderscopeBridgeOptions(string memoryName, byte maxChannelCount, ulong maxChannelBytes)
            : this(memoryName, System.IO.Path.GetTempPath(), maxChannelCount, maxChannelBytes) { }

        public unsafe ThunderscopeBridgeOptions(string memoryName, string path, byte maxChannelCount, ulong maxChannelBytes)
        {
            if (string.IsNullOrWhiteSpace(memoryName))
                throw new ArgumentNullException(nameof(memoryName));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            MemoryName = memoryName;
            Path = path;
            DataCapacityBytes = maxChannelCount * maxChannelBytes * 2;      // * 2 as there are 2 regions used in tick-tock fashion
            BridgeCapacityBytes = (ulong)sizeof(ThunderscopeBridgeHeader) + DataCapacityBytes;
            MaxChannelCount = maxChannelCount;
            MaxChannelBytes = maxChannelBytes;
        }
    }
}
