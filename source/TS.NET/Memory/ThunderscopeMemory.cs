using System.Runtime.InteropServices;
namespace TS.NET;

public unsafe struct ThunderscopeMemoryRegion
{
    public byte* Pointer;

    public ThunderscopeMemoryRegion(int segments)
    {
        Pointer = (byte*)NativeMemory.AlignedAlloc(ThunderscopeMemory.Length * (uint)segments, 4096);   // Intentionally not sbyte
    }

    public ThunderscopeMemory GetSegment(uint index)
    {
        return new ThunderscopeMemory()
        {
            Pointer = this.Pointer + (index * ThunderscopeMemory.Length)
        };
    }

    // https://tooslowexception.com/disposable-ref-structs-in-c-8-0/
    public void Dispose()
    {
        NativeMemory.AlignedFree(Pointer);
    }
}

public unsafe struct ThunderscopeMemory
{
    public const int Length = 1 << 23;     // 8388608 bytes
    public byte* Pointer;
    public Span<byte> SpanU8 { get { return new Span<byte>(Pointer, Length); } }
    public Span<sbyte> SpanI8 { get { return new Span<sbyte>(Pointer, Length); } }
}