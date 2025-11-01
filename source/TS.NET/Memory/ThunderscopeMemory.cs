using System.Runtime.InteropServices;

namespace TS.NET;

// PreLength is a small amount of memory before the data, to improve
// the efficiency of filters with block-based processing
// that need to copy history data to the beginning of the block.
public unsafe class ThunderscopeMemory
{
    // 1 << 23 = 8388608 (8388608 * I8) (4194304 * I16) [should be multiple of ThunderscopeMemoryRegion.Alignment]
    public const int Alignment = 4096;
    public const int MaximumDataByteWidth = sizeof(short);

    private byte* pointer;
    public int LengthBytes { get; private set; }
    public int PreLengthBytes { get; private set; }

    //public byte* Pointer { get { return pointer; } }
    public byte* DataLoadPointer { get { return pointer + PreLengthBytes; } }

    public ThunderscopeMemory(int lengthBytes)
    {
        LengthBytes = lengthBytes;
        PreLengthBytes = Alignment;
        pointer = (byte*)NativeMemory.AlignedAlloc((uint)(PreLengthBytes + LengthBytes), Alignment);   // Intentionally not sbyte
    }

    public Span<sbyte> DataSpanI8
    {
        get
        {
            return new Span<sbyte>(DataLoadPointer, LengthBytes);
        }
    }
    public Span<short> DataSpanI16
    {
        get
        {
            return new Span<short>(DataLoadPointer, LengthBytes / sizeof(short));
        }
    }

    //public Span<sbyte> FullSpanI8 { get { return new Span<sbyte>(Pointer, PreambleLength + DataLength); } }
    //public Span<short> FullSpanI16 { get { return new Span<short>(Pointer, PreambleLength + DataLength); } }

    // https://tooslowexception.com/disposable-ref-structs-in-c-8-0/
    // Don't need to dispose at the end of process:
    //    https://devblogs.microsoft.com/oldnewthing/20120105-00/?p=8683
    public void Dispose()
    {
        NativeMemory.AlignedFree(pointer);
    }
}