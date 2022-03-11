using TS.NET.Memory;

namespace TS.NET
{
    public sealed class ThunderscopeBridgeOptions
    {
        public string MemoryName { get; }
        public string Path { get; }
        public ulong BridgeCapacity { get; }
        public ulong DataCapacity { get; }

        public ThunderscopeBridgeOptions(string memoryName, ulong dataCapacity)
            : this(memoryName, System.IO.Path.GetTempPath(), dataCapacity) { }

        public unsafe ThunderscopeBridgeOptions(string memoryName, string path, ulong dataCapacity)
        {
            if(string.IsNullOrWhiteSpace(memoryName))
                throw new ArgumentNullException(nameof(memoryName));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            MemoryName = memoryName;
            Path = path;
            DataCapacity = dataCapacity;
            BridgeCapacity = (ulong)sizeof(ThunderscopeBridgeHeader) + dataCapacity;
        }
    }
}
