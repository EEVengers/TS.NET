using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET
{
    // Based on https://github.com/Cysharp/NativeMemoryArray
    public sealed unsafe class NativeMemoryAligned<T> : IDisposable
        where T : unmanaged
    {
        public static readonly NativeMemoryAligned<T> Empty;

        readonly long length;
        readonly bool addMemoryPressure;
        internal readonly byte* buffer;
        bool isDisposed;

        public long Length => length;

        static NativeMemoryAligned()
        {
            Empty = new NativeMemoryAligned<T>(0);
            Empty.Dispose();
        }

        public NativeMemoryAligned(long length, uint alignment = 4096, bool addMemoryPressure = false)
        {
            this.length = length;
            this.addMemoryPressure = addMemoryPressure;

            if (length == 0)
            {
                buffer = (byte*)Unsafe.AsPointer(ref Unsafe.NullRef<byte>());
            }
            else
            {
                var allocSize = length * Unsafe.SizeOf<T>();
                buffer = (byte*)NativeMemory.AlignedAlloc(checked((nuint)length), alignment);
                if (addMemoryPressure)
                {
                    GC.AddMemoryPressure(allocSize);
                }
            }
        }

        public ref T this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((ulong)index >= (ulong)length) throw new ArgumentOutOfRangeException();
                var memoryIndex = index * Unsafe.SizeOf<T>();
                return ref Unsafe.AsRef<T>(buffer + memoryIndex);
            }
        }

        public Span<T> AsSpan()
        {
            return AsSpan(0);
        }

        public Span<T> AsSpan(long start)
        {
            if ((ulong)start > (ulong)length) throw new ArgumentOutOfRangeException(nameof(start));
            return AsSpan(start, checked((int)(length - start)));
        }

        public Span<T> AsSpan(long start, int length)
        {
            if ((ulong)(start + length) > (ulong)this.length) throw new ArgumentOutOfRangeException(nameof(length));
            return new Span<T>(buffer + start * Unsafe.SizeOf<T>(), length);
        }

        public Memory<T> AsMemory()
        {
            return AsMemory(0);
        }

        public Memory<T> AsMemory(long start)
        {
            if ((ulong)start > (ulong)length) throw new ArgumentOutOfRangeException(nameof(start));
            return AsMemory(start, checked((int)(length - start)));
        }

        public Memory<T> AsMemory(long start, int length)
        {
            if ((ulong)(start + length) > (ulong)(this.length)) throw new ArgumentOutOfRangeException(nameof(length));
            return new PointerMemoryManager<T>(buffer + start * Unsafe.SizeOf<T>(), length).Memory;
        }

        public Stream AsStream()
        {
            return new UnmanagedMemoryStream(buffer, length * Unsafe.SizeOf<T>());
        }

        public Stream AsStream(long offset)
        {
            if ((ulong)offset > (ulong)length) throw new ArgumentOutOfRangeException(nameof(offset));
            return new UnmanagedMemoryStream(buffer + offset * Unsafe.SizeOf<T>(), length * Unsafe.SizeOf<T>());
        }

        public Stream AsStream(FileAccess fileAccess)
        {
            var len = length * Unsafe.SizeOf<T>();
            return new UnmanagedMemoryStream(buffer, len, len, fileAccess);
        }

        public Stream AsStream(long offset, FileAccess fileAccess)
        {
            if ((ulong)offset > (ulong)length) throw new ArgumentOutOfRangeException(nameof(offset));
            var len = length * Unsafe.SizeOf<T>();
            return new UnmanagedMemoryStream(buffer + offset * Unsafe.SizeOf<T>(), len, len, fileAccess);
        }

        public Stream AsStream(long offset, long length)
        {
            if ((ulong)offset > (ulong)this.length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset + length > this.length) throw new ArgumentOutOfRangeException(nameof(length));

            return new UnmanagedMemoryStream(buffer + offset * Unsafe.SizeOf<T>(), length * Unsafe.SizeOf<T>());
        }

        public Stream AsStream(long offset, long length, FileAccess fileAccess)
        {
            if ((ulong)offset > (ulong)this.length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset + length > this.length) throw new ArgumentOutOfRangeException(nameof(length));

            var len = length * Unsafe.SizeOf<T>();
            return new UnmanagedMemoryStream(buffer + offset * Unsafe.SizeOf<T>(), len, len, fileAccess);
        }

        public ref T GetPinnableReference()
        {
            if (length == 0)
            {
                return ref Unsafe.NullRef<T>();
            }
            return ref this[0];
        }

        public bool TryGetFullSpan(out Span<T> span)
        {
            if (length < int.MaxValue)
            {
                span = new Span<T>(buffer, (int)length);
                return true;
            }
            else
            {
                span = default;
                return false;
            }
        }

        public override string ToString()
        {
            return typeof(T).Name + "[" + length + "]";
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
                if (Unsafe.IsNullRef(ref Unsafe.AsRef<byte>(buffer))) return;
                NativeMemory.Free(buffer);
                if (addMemoryPressure)
                {
                    GC.RemoveMemoryPressure(length * Unsafe.SizeOf<T>());
                }
            }
        }

        ~NativeMemoryAligned()
        {
            DisposeCore();
        }
    }

    internal sealed unsafe class PointerMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        byte* pointer;
        int length;
        bool usingMemory;

        internal PointerMemoryManager(byte* pointer, int length)
        {
            this.pointer = pointer;
            this.length = length;
            usingMemory = false;
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override Span<T> GetSpan()
        {
            usingMemory = true;
            return new Span<T>(pointer, length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if ((uint)elementIndex >= (uint)length) throw new ArgumentOutOfRangeException();
            return new MemoryHandle(pointer + elementIndex * Unsafe.SizeOf<T>(), default, this);
        }

        public override void Unpin()
        {
        }

        public void AllowReuse()
        {
            usingMemory = false;
        }

        public void Reset(byte* pointer, int length)
        {
            if (usingMemory) throw new InvalidOperationException("Memory is using, can not reset.");
            this.pointer = pointer;
            this.length = length;
        }
    }
}
