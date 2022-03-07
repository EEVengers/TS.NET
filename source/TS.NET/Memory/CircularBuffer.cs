using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET
{
    public unsafe sealed class CircularBuffer
    {
        private readonly byte* buffer;

        public CircularBuffer(byte* buffer, long capacity)
        {
            this.buffer = buffer;
            Capacity = capacity;
        }

        internal long Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte* GetPointer(long offset)
        {
            AdjustedOffset(ref offset);
            return buffer + offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> Read(long offset, long length, Memory<byte> resultBuffer)
        {
            if (length == 0)
                return ReadOnlyMemory<byte>.Empty;

            if (length > resultBuffer.Length)
                length = resultBuffer.Length;

            AdjustedOffset(ref offset);
            fixed(byte* pinnedResultBuffer = resultBuffer.Span)
            {
                var sourcePtr = buffer + offset;

                var rightLength = Math.Min(Capacity - offset, length);
                if (rightLength > 0)
                {
                    Buffer.MemoryCopy(sourcePtr, pinnedResultBuffer, rightLength, rightLength);
                }

                var leftLength = length - rightLength;
                if (leftLength > 0)
                    Buffer.MemoryCopy(buffer, pinnedResultBuffer + rightLength, leftLength, leftLength);
            }

            return resultBuffer.Slice(0, (int)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(long offset, long length, byte* resultBuffer)
        {
            //if (length == 0)
            //    return ReadOnlyMemory<byte>.Empty;

            //if (length > resultBuffer.Length)
            //    length = resultBuffer.Length;

            AdjustedOffset(ref offset);
            //fixed (byte* pinnedResultBuffer = resultBuffer.Span)
           // {
                var sourcePtr = buffer + offset;

                var rightLength = Math.Min(Capacity - offset, length);
                if (rightLength > 0)
                {
                    Buffer.MemoryCopy(sourcePtr, resultBuffer, rightLength, rightLength);
                }

                var leftLength = length - rightLength;
                if (leftLength > 0)
                    Buffer.MemoryCopy(buffer, resultBuffer + rightLength, leftLength, leftLength);
           // }

            //return resultBuffer.Slice(0, (int)length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<byte> source, long offset)
        {
            fixed (byte* sourcePtr = &MemoryMarshal.GetReference(source))
                Write(sourcePtr, source.Length, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T source, long offset) where T : struct
        {
            Write((byte*)Unsafe.AsPointer(ref source), Unsafe.SizeOf<T>(), offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write(byte* sourcePtr, long sourceLength, long offset)
        {
            if (sourceLength == 0)
                return;

            AdjustedOffset(ref offset);
            var rightLength = Math.Min(Capacity - offset, sourceLength);
            Buffer.MemoryCopy(sourcePtr, buffer + offset, rightLength, rightLength);

            var leftLength = sourceLength - rightLength;
            if (leftLength > 0)
                Buffer.MemoryCopy(sourcePtr + rightLength, buffer, leftLength, leftLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(long offset, long length)
        {
            if (length == 0)
                return;

            AdjustedOffset(ref offset);
            var rightLength = Math.Min(Capacity - offset, length);
            Unsafe.InitBlock(buffer + offset, 0, (uint)rightLength);

            var leftLength = length - rightLength;
            if (leftLength > 0)
                Unsafe.InitBlock(buffer, 0, (uint)leftLength);
        }

        // internal for testing
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdjustedOffset(ref long offset)
            => offset %= Capacity;
    }
}
