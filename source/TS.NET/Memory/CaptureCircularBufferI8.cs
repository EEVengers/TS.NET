namespace TS.NET
{
    // Windows are copied out of multiple ChannelSampleCircularBufferI8 into this CaptureCircularBufferI8 when triggered, streamed or forced.
    // This allows time for UI consumers to request multiple captures, particularly if the capture length is short (e.g. 1000 samples at ~1M captures/sec).
    // When the user changes the capture length, this buffer will empty, and then configure to allow the maximum number of captures in the given memory.
    // When the user changes various settings, this buffer will empty.
    //
    // Needs to be thread safe for a single writer thread and a single reader thread. Multiple writers or readers not allowed.
    // The MMF becomes a pure data exchange mechanism.

    public interface ICaptureBufferConsumer<T>
    {
        Lock ReadLock { get; }
        int ChannelCount { get; }
        bool TryStartRead(out CaptureMetadata captureMetadata);
        ReadOnlySpan<T> GetReadBuffer(int channelIndex);
        void FinishRead();
    }

    public class CaptureCircularBufferI8 : IDisposable, ICaptureBufferConsumer<sbyte>
    {
        private readonly NativeMemoryAligned_Old<sbyte> buffer;
        private readonly Lock readLock = new();     // Configure/Reset/TryStartWrite/ResetIntervalStats happen on the same thread, so no need for a WriteLock
        public Lock ReadLock { get { return readLock; } }

        private long captureLengthBytes;
        private int channelCount;
        private int channelLengthBytes;

        private int maxCaptureCount;
        private int currentCaptureCount;
        private long captureTotal;
        private long captureDrops;
        private long captureReads;
        private long intervalCaptureTotal;      // Used for console messages
        private long intervalCaptureDrops;
        private long intervalCaptureReads;

        private long writeCaptureOffset;
        private long readCaptureOffset;
        private long wraparoundOffset;

        Dictionary<long, CaptureMetadata> captureMetadata;
        bool writeInProgress = false;
        bool readInProgress = false;

        // These properties can optionally be used with lock(){}, depending on importance of correctness
        public int ChannelCount { get { return channelCount; } }

        public long MaxCaptureCount { get { return maxCaptureCount; } }
        public long CurrentCaptureCount { get { return currentCaptureCount; } }
        public long CaptureTotal { get { return captureTotal; } }
        public long CaptureDrops { get { return captureDrops; } }
        public long CaptureReads { get { return captureReads; } }
        public long IntervalCaptureTotal { get { return intervalCaptureTotal; } }
        public long IntervalCaptureDrops { get { return intervalCaptureDrops; } }
        public long IntervalCaptureReads { get { return intervalCaptureReads; } }

        public CaptureCircularBufferI8(long totalBufferLength)
        {
            buffer = new NativeMemoryAligned_Old<sbyte>(totalBufferLength);
            captureMetadata = [];
        }

        public void Dispose()
        {
            buffer.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Configure(int channelCount, int channelLengthBytes)
        {
            var potentialCaptureLengthBytes = (long)channelLengthBytes * channelCount;
            if (potentialCaptureLengthBytes > buffer.Length)
                throw new ArgumentOutOfRangeException("Requested configuration exceeds memory size.");

            lock (readLock)
            {
                captureLengthBytes = potentialCaptureLengthBytes;
                this.channelCount = channelCount;
                this.channelLengthBytes = channelLengthBytes;

                maxCaptureCount = (int)(buffer.Length / captureLengthBytes);
                if (maxCaptureCount > 10)
                {
                    maxCaptureCount = 10;   // Coerce for now until "stale data" logic implemented where UI is too slow to pull all the data
                }
                wraparoundOffset = maxCaptureCount * captureLengthBytes;
                Reset();
            }
        }

        public void Reset()
        {
            lock (readLock)
            {
                currentCaptureCount = 0;
                captureTotal = 0;
                captureDrops = 0;
                captureReads = 0;

                writeCaptureOffset = 0;
                readCaptureOffset = 0;
                captureMetadata = [];
            }
        }

        public bool TryStartWrite()
        {
            if (writeInProgress)
                throw new InvalidOperationException();

            captureTotal++;
            intervalCaptureTotal++;
            if (currentCaptureCount == maxCaptureCount)
            {
                captureDrops++;
                intervalCaptureDrops++;
                return false;
            }
            writeInProgress = true;
            return true;
        }

        public void FinishWrite(CaptureMetadata captureMetadata)
        {
            lock (readLock)
            {
                this.captureMetadata[writeCaptureOffset] = captureMetadata;
                currentCaptureCount++;
                writeCaptureOffset += captureLengthBytes;
                if (writeCaptureOffset >= wraparoundOffset)
                    writeCaptureOffset = 0;
                writeInProgress = false;
            }
        }

        public Span<sbyte> GetWriteBuffer(int channelIndex)
        {
            if (channelIndex >= channelCount)
                throw new ArgumentException();
            int offset = channelIndex * channelLengthBytes;
            return buffer.AsSpan(writeCaptureOffset + offset, channelLengthBytes);
        }

        public bool TryStartRead(out CaptureMetadata captureMetadata)
        {
            if (readInProgress)
                throw new InvalidOperationException();

            if (currentCaptureCount > 0)
            {
                captureMetadata = this.captureMetadata[readCaptureOffset];
                readInProgress = true;
                return true;
            }
            else
            {
                captureMetadata = default;
                return false;
            }
        }

        public void FinishRead()
        {
            currentCaptureCount--;
            readCaptureOffset += captureLengthBytes;
            if (readCaptureOffset >= wraparoundOffset)
                readCaptureOffset = 0;
            captureReads++;
            intervalCaptureReads++;
            readInProgress = false;
        }

        public ReadOnlySpan<sbyte> GetReadBuffer(int channelIndex)
        {
            if (channelIndex >= channelCount)
                throw new ArgumentException();
            int offset = channelIndex * channelLengthBytes;
            return buffer.AsSpan(readCaptureOffset + offset, channelLengthBytes);
        }

        public void ResetIntervalStats()
        {
            intervalCaptureTotal = 0;
            intervalCaptureDrops = 0;
            intervalCaptureReads = 0;
        }
    }

    //public struct CaptureMetadata
    //{
    //    public bool Triggered;
    //    public int TriggerChannelCaptureIndex;
    //    public ThunderscopeHardwareConfig HardwareConfig;
    //    public ThunderscopeProcessingConfig ProcessingConfig;
    //}
}