namespace TS.NET
{
    public unsafe sealed class AcquisitionCircularBuffer : IDisposable
    {
        private readonly NativeMemoryAligned memory;
        private int capacity;
        private int tail;
        private long totalWritten;

        public AcquisitionCircularBuffer(int maxChannelLength, ThunderscopeDataType maxDataType)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maxChannelLength, 2000000000);
            capacity = maxChannelLength + ThunderscopeMemory.Length;
            memory = new NativeMemoryAligned((long)capacity * maxDataType.ByteWidth());
            Reset();
        }

        public void Dispose()
        {
            memory.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Reset()
        {
            tail = 0;
            totalWritten = 0;
            memory.Clear();
        }

        public void Write<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            if (data.Length > capacity)
                throw new Exception($"AcquisitionCircularBuffer too small to write {data.Length}");

            int firstCopy = Math.Min(data.Length, capacity - tail);
            if (firstCopy > 0)
                data.Slice(0, firstCopy).CopyTo(memory.AsSpan<T>(tail, firstCopy));

            int remainingCopy = data.Length - firstCopy;
            if (remainingCopy > 0)
                data.Slice(firstCopy, remainingCopy).CopyTo(memory.AsSpan<T>(0, remainingCopy));
            tail = (tail + data.Length) % capacity;

            totalWritten += data.Length;
        }

        public void Read<T>(Span<T> data, int endOffset) where T : unmanaged
        {
            if (data.Length > capacity)
                throw new Exception($"AcquisitionCircularBuffer too small to read {data.Length}");

            int offsetTail = 0;
            if (endOffset <= tail)
                offsetTail = (tail - endOffset);        // Offset tail being the index of the last byte we want
            else
                offsetTail = capacity - (endOffset - tail);
            //uint offsetTail = tail % capacity;
            int firstCopy = Math.Min(data.Length, offsetTail);
            if (firstCopy > 0)
                memory.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(data.Slice(data.Length - firstCopy));

            int remainingCopy = data.Length - firstCopy;
            if (remainingCopy > 0)
                memory.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(data);
        }
    }
}
