using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Total memory can be larger than Int32.Max on 64-bit system, as long as the data read/write is up to Int32.Max.

namespace TS.NET
{
    // Based on https://github.com/Cysharp/NativeMemoryArray
    public sealed unsafe class NativeMemoryAligned : IDisposable
    {
        private readonly long memoryLengthBytes;
        private readonly bool addMemoryPressure;
        private readonly void* memory;
        private bool isDisposed;

        public long LengthBytes => memoryLengthBytes;

        public NativeMemoryAligned(long memoryLengthBytes, uint alignment = 4096, bool addMemoryPressure = false)
        {
            this.memoryLengthBytes = memoryLengthBytes;
            this.addMemoryPressure = addMemoryPressure;

            if (memoryLengthBytes == 0)
            {
                memory = Unsafe.AsPointer(ref Unsafe.NullRef<byte>());
            }
            else
            {
                memory = NativeMemory.AlignedAlloc(checked((nuint)memoryLengthBytes), alignment);
                if (addMemoryPressure)
                {
                    GC.AddMemoryPressure(memoryLengthBytes);
                }
            }
        }

        public Span<T> AsSpan<T>(long start, int length) where T : unmanaged
        {
            var startByte = start * sizeof(T);
            var lengthByte = length * sizeof(T);

            if ((startByte + lengthByte) > memoryLengthBytes) 
                throw new ArgumentOutOfRangeException();
            return new Span<T>((byte*)memory + startByte, length);
        }

        public void Clear()
        {
            NativeMemory.Clear(memory, checked((nuint)memoryLengthBytes));
        }

        public void Dispose()
        {
            DisposeCore();
            GC.SuppressFinalize(this);
        }

        void DisposeCore()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                if (Unsafe.IsNullRef(ref Unsafe.AsRef<byte>(memory))) return;
                NativeMemory.AlignedFree(memory);
                if (addMemoryPressure)
                {
                    GC.RemoveMemoryPressure(memoryLengthBytes);
                }
            }
        }

        ~NativeMemoryAligned()
        {
            DisposeCore();
        }
    }
}
