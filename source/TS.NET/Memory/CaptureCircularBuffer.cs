using Microsoft.Extensions.Logging;

namespace TS.NET
{
    // Windows are copied out of multiple AcquisitionCircularBuffer into this CaptureCircularBuffer when triggered, streamed or forced.
    // This allows time for UI consumers to request multiple captures, particularly if the capture length is short (e.g. 1000 samples at ~1M captures/sec).
    // When the user changes the capture length, this buffer will empty, and then configure to allow the maximum number of captures in the given memory.
    // When the user changes various settings, this buffer will empty.
    //
    // Needs to be thread safe for a single writer thread and a single reader thread. Multiple writers or readers not allowed.

    public interface ICaptureBufferReader
    {
        Lock ReadLock { get; }
        int ChannelCount { get; }
        bool TryStartRead(out CaptureMetadata captureMetadata);
        ReadOnlySpan<T> GetChannelReadBuffer<T>(int channelIndex) where T : unmanaged;
        void FinishRead();
    }

    public class CaptureCircularBuffer : IDisposable, ICaptureBufferReader
    {
        private readonly ILogger logger;
        private readonly NativeMemoryAligned buffer;
        private readonly Lock readLock = new();     // Configure/Reset/TryStartWrite/ResetIntervalStats happen on the same thread, so no need for a WriteLock
        public Lock ReadLock { get { return readLock; } }

        private int channelCount;
        private int channelCaptureLength;

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

        // Used for capture metadata
        private double cachedCapturesPerSec = 0;
        DateTimeOffset internalIntervalCaptureStartUtc = DateTimeOffset.UtcNow;
        private long internalIntervalCaptureTotal;      

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

        public CaptureCircularBuffer(ILogger logger, long totalBufferLengthBytes)
        {
            this.logger = logger;
            buffer = new NativeMemoryAligned(totalBufferLengthBytes);
            captureMetadata = [];
        }

        // Don't need to dispose at the end of process:
        //    https://devblogs.microsoft.com/oldnewthing/20120105-00/?p=8683
        public void Dispose()
        {
            buffer.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Configure(int channelCount, int channelCaptureLength, ThunderscopeDataType dataType)
        {
            if (channelCount < 1)
                channelCount = 1;
            var potentialTotalCaptureLength = (long)channelCaptureLength * channelCount;
            var potentialTotalCaptureLengthBytes = potentialTotalCaptureLength * dataType.ByteWidth();
            if (potentialTotalCaptureLengthBytes > buffer.LengthBytes)
                throw new ArgumentOutOfRangeException("Requested configuration exceeds memory size.");

            lock (readLock)
            {
                this.channelCount = channelCount;
                this.channelCaptureLength = channelCaptureLength;

                maxCaptureCount = (int)(buffer.LengthBytes / potentialTotalCaptureLengthBytes);

                int uiWaveformDisplayRate = 30;
                if (maxCaptureCount > uiWaveformDisplayRate)
                {
                    maxCaptureCount = uiWaveformDisplayRate;   // Coerce for now until flow control mechanism in place
                    logger.LogDebug($"CaptureCircularBuffer maxCaptureCount coerced to {maxCaptureCount}");
                }
                wraparoundOffset = maxCaptureCount * potentialTotalCaptureLength;

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

            // cachedInternalCaptureRatePerSecond logic, used for CaptureMetadata
            var now = DateTimeOffset.UtcNow;
            var duration = now.Subtract(internalIntervalCaptureStartUtc).TotalSeconds;
            if (duration > 1)
            {
                cachedCapturesPerSec = internalIntervalCaptureTotal / duration;
                internalIntervalCaptureStartUtc = now;
                internalIntervalCaptureTotal = 0;
            }
            internalIntervalCaptureTotal++;

            lock(readLock)
            {
                if (currentCaptureCount == maxCaptureCount)
                {
                    captureDrops++;
                    intervalCaptureDrops++;
                    FinishRead(drop: true);       // Remove oldest buffer
                }
            }
            writeInProgress = true;
            return true;
        }

        public Span<T> GetChannelWriteBuffer<T>(int channelIndex) where T : unmanaged
        {
            if (channelIndex >= channelCount)
                throw new ArgumentException();
            int channelOffset = channelIndex * channelCaptureLength;
            return buffer.AsSpan<T>(writeCaptureOffset + channelOffset, channelCaptureLength);
        }

        public void FinishWrite(CaptureMetadata captureMetadata)
        {
            lock (readLock)
            {
                captureMetadata.CapturesPerSec = cachedCapturesPerSec;
                this.captureMetadata[writeCaptureOffset] = captureMetadata;
                currentCaptureCount++;
                writeCaptureOffset += (channelCount * channelCaptureLength);
                if (writeCaptureOffset >= wraparoundOffset)
                    writeCaptureOffset = 0;
                writeInProgress = false;
            }
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

        public ReadOnlySpan<T> GetChannelReadBuffer<T>(int channelIndex) where T : unmanaged
        {
            if (channelIndex >= channelCount)
                throw new ArgumentException(nameof(channelIndex));
            int channelOffset = channelIndex * channelCaptureLength;
            return buffer.AsSpan<T>(readCaptureOffset + channelOffset, channelCaptureLength);
        }

        public void FinishRead()
        {
            FinishRead(drop: false);
        }

        private void FinishRead(bool drop = false)
        {
            currentCaptureCount--;
            readCaptureOffset += (channelCount * channelCaptureLength);
            if (readCaptureOffset >= wraparoundOffset)
                readCaptureOffset = 0;
            captureReads++;
            if (!drop)
            {
                intervalCaptureReads++;
            }
            readInProgress = false;
        }

        public void ResetIntervalStats()
        {
            intervalCaptureTotal = 0;
            intervalCaptureDrops = 0;
            intervalCaptureReads = 0;
        }
    }

    public struct CaptureMetadata
    {
        public bool Triggered;
        public int TriggerChannelCaptureIndex;
        public ThunderscopeHardwareConfig HardwareConfig;
        public ThunderscopeProcessingConfig ProcessingConfig;
        public double CapturesPerSec;
    }
}