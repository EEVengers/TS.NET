// https://github.com/cloudtoid/interprocess
using static Cloudtoid.Contract;
using SysPath = System.IO.Path;
using TS.NET.Memory;

namespace TS.NET
{
    public sealed class PostboxOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostboxOptions"/> class.
        /// </summary>
        /// <param name="memoryName">The unique name of the memory.</param>
        /// <param name="bytesCapacity">The maximum capacity of the memory in bytes. This should be at least 16 bytes long and in the multiples of 8</param>
        public PostboxOptions(string memoryName, long bytesCapacity)
            : this(memoryName, SysPath.GetTempPath(), bytesCapacity)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostboxOptions"/> class.
        /// </summary>
        /// <param name="memoryName">The unique name of the memory.</param>
        /// <param name="path">The path to the directory/folder in which the memory mapped and other files are stored in</param>
        /// <param name="bytesCapacity">The maximum capacity of the memory in bytes. This should be at least 16 bytes long and in the multiples of 8</param>
        public unsafe PostboxOptions(string memoryName, string path, long bytesCapacity)
        {
            MemoryName = CheckNonEmpty(memoryName, nameof(memoryName));
            Path = CheckValue(path, nameof(path));
            BytesCapacity = sizeof(MemoryHeader) + bytesCapacity;
            CheckParam((BytesCapacity % 8) == 0, nameof(memoryName), "bytesCapacity should be a multiple of 8 (8 bytes = 64 bits).");
        }

        /// <summary>
        /// Gets the unique name of the memory.
        /// </summary>
        public string MemoryName { get; }

        /// <summary>
        /// Gets the path to the directory/folder in which the memory mapped and other files are stored in.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the maximum capacity of the memory in bytes.
        /// </summary>
        public long BytesCapacity { get; }
    }
}
