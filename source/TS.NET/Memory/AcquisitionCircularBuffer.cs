namespace TS.NET
{
    public unsafe sealed class AcquisitionCircularBuffer : IDisposable
    {
        private readonly NativeMemoryAligned memory1;
        private readonly NativeMemoryAligned memory2;
        private readonly NativeMemoryAligned memory3;
        private readonly NativeMemoryAligned memory4;
        private int capacity;
        private int tail;
        private int samplesInBufferPerChannel;
        private ulong streamEndIndex;

        public int SamplesInBufferPerChannel { get { return samplesInBufferPerChannel; } }
        //public ulong BufferSampleStartIndex { get { return streamEndIndex - (ulong)samplesInBufferPerChannel; } }
        //public ulong BufferSampleEndIndex { get { return streamEndIndex; } }

        public AcquisitionCircularBuffer(int maxChannelLength, int segmentLengthBytes, ThunderscopeDataType maxDataType)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maxChannelLength, 2_000_000_000);
            capacity = maxChannelLength + segmentLengthBytes;       // Be careful, if insufficient capacity, waveform discontinuities occur
            memory1 = new NativeMemoryAligned((long)capacity * maxDataType.ByteWidth());
            memory2 = new NativeMemoryAligned((long)capacity * maxDataType.ByteWidth());
            memory3 = new NativeMemoryAligned((long)capacity * maxDataType.ByteWidth());
            memory4 = new NativeMemoryAligned((long)capacity * maxDataType.ByteWidth());
            Reset();
        }

        public void Dispose()
        {
            memory1.Dispose();
            memory2.Dispose();
            memory3.Dispose();
            memory4.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Reset()
        {
            tail = 0;
            samplesInBufferPerChannel = 0;
            memory1.Clear();
            memory2.Clear();
            memory3.Clear();
            memory4.Clear();
        }

        public void Write1Channel<T>(ReadOnlySpan<T> channel1, ulong sampleStartIndex) where T : unmanaged
        {
            var length = channel1.Length;

            if (length > capacity)
                throw new Exception($"AcquisitionCircularBuffer too small to write {length}");

            streamEndIndex = sampleStartIndex + (ulong)length;

            int firstCopy = Math.Min(length, capacity - tail);
            if (firstCopy > 0)
                channel1.Slice(0, firstCopy).CopyTo(memory1.AsSpan<T>(tail, firstCopy));

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
                channel1.Slice(firstCopy, remainingCopy).CopyTo(memory1.AsSpan<T>(0, remainingCopy));
            tail = (tail + length) % capacity;

            samplesInBufferPerChannel += length;
            if (samplesInBufferPerChannel > capacity)
                samplesInBufferPerChannel = capacity;
        }

        public void Write2Channel<T>(ReadOnlySpan<T> channel1, ReadOnlySpan<T> channel2, ulong sampleStartIndex) where T : unmanaged
        {
            if (channel1.Length != channel2.Length)
                throw new ThunderscopeException("Channel lengths don't match");

            var length = channel1.Length;

            if (length > capacity)
                throw new ThunderscopeException($"AcquisitionCircularBuffer too small to write {length} samples");

            streamEndIndex = sampleStartIndex + (ulong)length;

            int firstCopy = Math.Min(length, capacity - tail);
            if (firstCopy > 0)
            {
                channel1.Slice(0, firstCopy).CopyTo(memory1.AsSpan<T>(tail, firstCopy));
                channel2.Slice(0, firstCopy).CopyTo(memory2.AsSpan<T>(tail, firstCopy));
            }

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
            {
                channel1.Slice(firstCopy, remainingCopy).CopyTo(memory1.AsSpan<T>(0, remainingCopy));
                channel2.Slice(firstCopy, remainingCopy).CopyTo(memory2.AsSpan<T>(0, remainingCopy));
            }
            tail = (tail + length) % capacity;

            samplesInBufferPerChannel += length;
            if (samplesInBufferPerChannel > capacity)
                samplesInBufferPerChannel = capacity;
        }

        public void Write4Channel<T>(ReadOnlySpan<T> channel1, ReadOnlySpan<T> channel2, ReadOnlySpan<T> channel3, ReadOnlySpan<T> channel4, ulong sampleStartIndex) where T : unmanaged
        {
            if (channel1.Length != channel2.Length || channel2.Length != channel3.Length || channel3.Length != channel4.Length)
                throw new ThunderscopeException("Channel lengths don't match");

            var length = channel1.Length;

            if (length > capacity)
                throw new ThunderscopeException($"AcquisitionCircularBuffer too small to write {length} samples");

            streamEndIndex = sampleStartIndex + (ulong)length;

            int firstCopy = Math.Min(length, capacity - tail);
            if (firstCopy > 0)
            {
                channel1.Slice(0, firstCopy).CopyTo(memory1.AsSpan<T>(tail, firstCopy));
                channel2.Slice(0, firstCopy).CopyTo(memory2.AsSpan<T>(tail, firstCopy));
                channel3.Slice(0, firstCopy).CopyTo(memory3.AsSpan<T>(tail, firstCopy));
                channel4.Slice(0, firstCopy).CopyTo(memory4.AsSpan<T>(tail, firstCopy));
            }

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
            {
                channel1.Slice(firstCopy, remainingCopy).CopyTo(memory1.AsSpan<T>(0, remainingCopy));
                channel2.Slice(firstCopy, remainingCopy).CopyTo(memory2.AsSpan<T>(0, remainingCopy));
                channel3.Slice(firstCopy, remainingCopy).CopyTo(memory3.AsSpan<T>(0, remainingCopy));
                channel4.Slice(firstCopy, remainingCopy).CopyTo(memory4.AsSpan<T>(0, remainingCopy));
            }
            tail = (tail + length) % capacity;

            samplesInBufferPerChannel += length;
            if (samplesInBufferPerChannel > capacity)
                samplesInBufferPerChannel = capacity;
        }

        public void Read1Channel<T>(Span<T> channel1, ulong captureEndIndex) where T : unmanaged
        {
            var length = channel1.Length;

            if (length > capacity)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} too small to read {length} samples");
            if (length > samplesInBufferPerChannel)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} does not contain {length} samples");

            int endOffset = (int)(streamEndIndex - captureEndIndex);

            int offsetTail = 0;
            if (endOffset <= tail)
                offsetTail = (tail - endOffset);        // Offset tail being the index of the last byte we want
            else
                offsetTail = capacity - (endOffset - tail);
            //uint offsetTail = tail % capacity;
            int firstCopy = Math.Min(length, offsetTail);
            if (firstCopy > 0)
                memory1.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel1.Slice(length - firstCopy));

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
                memory1.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel1);

            samplesInBufferPerChannel -= length;
        }

        public void Read2Channel<T>(Span<T> channel1, Span<T> channel2, ulong captureEndIndex) where T : unmanaged
        {
            if (channel1.Length != channel2.Length)
                throw new ThunderscopeException("Channel lengths don't match");

            var length = channel1.Length;

            if (length > capacity)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} too small to read {length} samples");
            if (length > samplesInBufferPerChannel)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} does not contain {length} samples");

            int endOffset = (int)(streamEndIndex - captureEndIndex);

            int offsetTail = 0;
            if (endOffset <= tail)
                offsetTail = (tail - endOffset);        // Offset tail being the index of the last byte we want
            else
                offsetTail = capacity - (endOffset - tail);
            //uint offsetTail = tail % capacity;
            int firstCopy = Math.Min(length, offsetTail);
            if (firstCopy > 0)
            {
                memory1.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel1.Slice(length - firstCopy));
                memory2.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel2.Slice(length - firstCopy));
            }

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
            {
                memory1.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel1);
                memory2.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel2);
            }

            samplesInBufferPerChannel -= length;
        }

        public void Read3Channel<T>(Span<T> channel1, Span<T> channel2, Span<T> channel3, ulong captureEndIndex) where T : unmanaged
        {
            if (channel1.Length != channel2.Length || channel2.Length != channel3.Length)
                throw new ThunderscopeException("Channel lengths don't match");

            var length = channel1.Length;

            if (length > capacity)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} too small to read {length} samples");
            if (length > samplesInBufferPerChannel)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} does not contain {length} samples");

            int endOffset = (int)(streamEndIndex - captureEndIndex);

            int offsetTail = 0;
            if (endOffset <= tail)
                offsetTail = (tail - endOffset);        // Offset tail being the index of the last byte we want
            else
                offsetTail = capacity - (endOffset - tail);
            //uint offsetTail = tail % capacity;
            int firstCopy = Math.Min(length, offsetTail);
            if (firstCopy > 0)
            {
                memory1.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel1.Slice(length - firstCopy));
                memory2.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel2.Slice(length - firstCopy));
                memory3.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel3.Slice(length - firstCopy));
            }

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
            {
                memory1.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel1);
                memory2.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel2);
                memory3.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel3);
            }

            samplesInBufferPerChannel -= length;

        }
        public void Read4Channel<T>(Span<T> channel1, Span<T> channel2, Span<T> channel3, Span<T> channel4, ulong captureEndIndex) where T : unmanaged
        {
            if (channel1.Length != channel2.Length || channel2.Length != channel3.Length || channel3.Length != channel4.Length)
                throw new ThunderscopeException("Channel lengths don't match");

            var length = channel1.Length;

            if (length > capacity)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} too small to read {length} samples");
            if (length > samplesInBufferPerChannel)
                throw new Exception($"{nameof(AcquisitionCircularBuffer)} does not contain {length} samples");

            int endOffset = (int)(streamEndIndex - captureEndIndex);

            int offsetTail = 0;
            if (endOffset <= tail)
                offsetTail = (tail - endOffset);        // Offset tail being the index of the last byte we want
            else
                offsetTail = capacity - (endOffset - tail);
            //uint offsetTail = tail % capacity;
            int firstCopy = Math.Min(length, offsetTail);
            if (firstCopy > 0)
            {
                memory1.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel1.Slice(length - firstCopy));
                memory2.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel2.Slice(length - firstCopy));
                memory3.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel3.Slice(length - firstCopy));
                memory4.AsSpan<T>(offsetTail - firstCopy, firstCopy).CopyTo(channel4.Slice(length - firstCopy));
            }

            int remainingCopy = length - firstCopy;
            if (remainingCopy > 0)
            {
                memory1.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel1);
                memory2.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel2);
                memory3.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel3);
                memory4.AsSpan<T>(capacity - remainingCopy, remainingCopy).CopyTo(channel4);
            }

            samplesInBufferPerChannel -= length;
        }
    }
}
