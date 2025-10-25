using System.Runtime.InteropServices;

namespace TS.NET;

public unsafe class ThunderscopeMemoryRegion
{
    private readonly int segmentLengthBytes;
    private readonly int segmentPreLengthBytes;

    public const int Alignment = 4096;
    public byte* RegionPointer;

    public ThunderscopeMemoryRegion(int segments, int segmentLengthBytes)
    {
        this.segmentPreLengthBytes = Alignment; // Add this on to the beginning segment so that every segment has a pre-data region.
        this.segmentLengthBytes = segmentLengthBytes;
        var regionLengthBytes = segmentPreLengthBytes + segmentLengthBytes * segments;
        RegionPointer = (byte*)NativeMemory.AlignedAlloc((uint)regionLengthBytes, Alignment);   // Intentionally not sbyte
    }

    public ThunderscopeMemory GetSegment(int index)
    {
        var segment = new ThunderscopeMemory(segmentLengthBytes, segmentPreLengthBytes);
        segment.Load(RegionPointer + (index * segmentLengthBytes));
        return segment;
    }

    // https://tooslowexception.com/disposable-ref-structs-in-c-8-0/
    // Don't need to dispose at the end of process:
    //    https://devblogs.microsoft.com/oldnewthing/20120105-00/?p=8683
    public void Dispose()
    {
        NativeMemory.AlignedFree(RegionPointer);
    }
}

//public unsafe struct ThunderscopeMemory
//{
//    public const int Length = 1 << 23;     // 8388608 bytes
//    public byte* Pointer;
//    public Span<byte> SpanU8 { get { return new Span<byte>(Pointer, Length); } }
//    public Span<sbyte> SpanI8 { get { return new Span<sbyte>(Pointer, Length); } }
//}

// PreLength is a small amount of memory before the data, to improve
// the efficiency of pre-processing filters with block-based processing
// that need to copy history data to the beginning of the block.
public unsafe class ThunderscopeMemory
{
    // 1 << 23 = 8388608 (8388608 * I8) (4194304 * I16) [should be multiple of ThunderscopeMemoryRegion.Alignment]

    public const int MaximumDataByteWidth = sizeof(short);

    private byte* SegmentPointer;
    public int LengthBytes { get; private set; }
    public int PreLengthBytes { get; private set; }

    public byte* DataLoadPointer { get { return SegmentPointer; } }

    public ThunderscopeMemory(int lengthBytes, int preLengthBytes)
    {
        LengthBytes = lengthBytes;
        PreLengthBytes = preLengthBytes;
    }

    public Span<sbyte> DataSpanI8
    {
        get
        {
            return new Span<sbyte>(SegmentPointer, LengthBytes);
        }
    }
    public Span<short> DataSpanI16
    {
        get
        {
            return new Span<short>(SegmentPointer, LengthBytes / sizeof(short));
        }
    }

    //public Span<sbyte> FullSpanI8 { get { return new Span<sbyte>(BasePointer, PreambleLength + DataLength); } }
    //public Span<short> FullSpanI16 { get { return new Span<short>(BasePointer, PreambleLength + DataLength); } }

    public void Load(byte* basePointer)
    {
        SegmentPointer = basePointer;
    }

    public void Reset()
    {
        // no-op, delete later
        //PreambleUsedLength = 0;
    }
}