using System.Runtime.InteropServices;

namespace TS.NET.Memory
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal struct MemoryHeader
    {
        [FieldOffset(0)]
        internal byte State;
    }

    internal enum PostboxState
    {
        Empty = 0,      // Writing is allowed
        Full = 1,        // Writing is blocked, waiting for reader to set back to Unset
    }
}