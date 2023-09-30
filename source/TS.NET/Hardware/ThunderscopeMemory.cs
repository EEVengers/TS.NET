using System;
using System.Runtime.InteropServices;
namespace TS.NET;

public unsafe struct ThunderscopeMemory
{
    public const uint Length = 1 << 23;     // 8388608 bytes
    public byte* Pointer;
    public Span<sbyte> Span { get { return new Span<sbyte>(Pointer, (int)Length); } }

    public ThunderscopeMemory()
    {
        Pointer = (byte*)NativeMemory.AlignedAlloc(Length, 4096);   // Intentionally not sbyte
    }

    // https://tooslowexception.com/disposable-ref-structs-in-c-8-0/
    public void Dispose()
    {
        NativeMemory.AlignedFree(Pointer);
    }
}