using System.Runtime.InteropServices;

namespace TS.NET;

public unsafe class ThunderscopeMemoryRegion
{
    private readonly uint segmentLength;

    public const int Alignment = 4096;
    public byte* Pointer;

    public ThunderscopeMemoryRegion(int segments)
    {
        segmentLength = (ThunderscopeMemory.PreambleLength + ThunderscopeMemory.DataLength) * ThunderscopeMemory.MaximumDataByteWidth;
        Pointer = (byte*)NativeMemory.AlignedAlloc(segmentLength * (uint)segments, Alignment);   // Intentionally not sbyte
    }

    public ThunderscopeMemory GetSegment(int index)
    {
        var segment = new ThunderscopeMemory();
        segment.Load(Pointer + (index * segmentLength));
        return segment;
    }

    // https://tooslowexception.com/disposable-ref-structs-in-c-8-0/
    public void Dispose()
    {
        NativeMemory.AlignedFree(Pointer);
    }
}

//public unsafe struct ThunderscopeMemory
//{
//    public const int Length = 1 << 23;     // 8388608 bytes
//    public byte* Pointer;
//    public Span<byte> SpanU8 { get { return new Span<byte>(Pointer, Length); } }
//    public Span<sbyte> SpanI8 { get { return new Span<sbyte>(Pointer, Length); } }
//}

// Preamble is a small amount of memory before the data, to improve
// the efficiency of pre-processing filters with block-based processing
// that need to copy history data to the beginning of the block.
public unsafe class ThunderscopeMemory
{
    // 1 << 23 = 8388608 (8388608 * I8) (4194304 * I16) [should be multiple of ThunderscopeMemoryRegion.Alignment]

    public const int MaximumDataByteWidth = sizeof(short);
    public const int PreambleLength = ThunderscopeMemoryRegion.Alignment;
    public const int DataLength = (1 << 23);

    private const int PreambleBytes = PreambleLength * MaximumDataByteWidth;
    //private const int DataLengthBytes = DataLength * MaximumDataByteWidth;

    private byte* BasePointer;

    public byte* DataLoadPointer { get { return BasePointer + PreambleBytes; } }

    public int PreambleUsedLength;

    public Span<sbyte> DataSpanI8
    {
        get
        {
            return new Span<sbyte>(BasePointer + PreambleBytes - PreambleUsedLength, DataLength);
        }
    }
    public Span<short> DataSpanI16
    {
        get
        {
            return new Span<short>(BasePointer + PreambleBytes - (PreambleUsedLength * sizeof(short)), DataLength);
        }
    }

    public Span<sbyte> FullSpanI8 { get { return new Span<sbyte>(BasePointer, PreambleLength + DataLength); } }
    public Span<short> FullSpanI16 { get { return new Span<short>(BasePointer, PreambleLength + DataLength); } }

    public void Load(byte* basePointer)
    {
        BasePointer = basePointer;
    }

    public void Reset()
    {
        PreambleUsedLength = 0;
    }
}