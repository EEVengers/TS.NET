using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET
{
    public interface ICaptureBufferManagerReader
    {
        bool TryStartRead(out CaptureBuffer? buffer);
        void FinishRead();
    }

    public interface ICaptureBufferManagerWriter
    {
        bool TryStartWrite(out CaptureBuffer? buffer);
        void FinishWrite(CaptureMetadata captureMetadata);
    }

    public struct CaptureMetadata
    {
        public bool Triggered;
        public int TriggerChannelCaptureIndex;
        public ThunderscopeHardwareConfig HardwareConfig;
        public ThunderscopeProcessingConfig ProcessingConfig;
        public double CapturesPerSec;
    }

    public class CaptureBuffer(int managerConfiguration, int channelCount, int channelLength, ThunderscopeDataType dataType, byte[] buffer)
    {
        public int ManagerConfiguration { get; } = managerConfiguration;       // Used to discard the capture buffer if it's returned after a reconfiguration happened
        public int ChannelCount { get; } = channelCount;
        public int ChannelLength { get; } = channelLength;
        public ThunderscopeDataType DataType { get; } = dataType;
        public byte[] Buffer { get; } = buffer;
        public CaptureMetadata Metadata { get; set; } = new CaptureMetadata();

        public Span<T> GetChannelWriteBuffer<T>(int channelIndex) where T : unmanaged
        {
            int byteWidth = DataType.ByteWidth();
            int elementSize = Unsafe.SizeOf<T>();
            if (byteWidth != elementSize)
                throw new ArgumentOutOfRangeException(nameof(DataType));

            var byteBuffer = GetChannelWriteByteBuffer(channelIndex);
            return MemoryMarshal.Cast<byte, T>(byteBuffer);
        }

        public Span<byte> GetChannelWriteByteBuffer(int channelIndex)
        {
            if (channelIndex < 0 || channelIndex >= ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            int byteWidth = DataType.ByteWidth();
            if (byteWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(DataType));
            int channelByteLength = ChannelLength * byteWidth;
            int offset = channelIndex * channelByteLength;
            if (offset < 0 || offset + channelByteLength > Buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            return new Span<byte>(Buffer, offset, channelByteLength);
        }

        public ReadOnlySpan<T> GetChannelReadBuffer<T>(int channelIndex) where T : unmanaged
        {
            int byteWidth = DataType.ByteWidth();
            int elementSize = Unsafe.SizeOf<T>();
            if (byteWidth != elementSize)
                throw new ArgumentOutOfRangeException(nameof(DataType));

            var byteBuffer = GetChannelReadByteBuffer(channelIndex);
            return MemoryMarshal.Cast<byte, T>(byteBuffer);
        }

        public ReadOnlySpan<byte> GetChannelReadByteBuffer(int channelIndex)
        {
            if (channelIndex < 0 || channelIndex >= ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            int byteWidth = DataType.ByteWidth();
            if (byteWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(DataType));
            int channelByteLength = ChannelLength * byteWidth;
            int offset = channelIndex * channelByteLength;
            if (offset < 0 || offset + channelByteLength > Buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            return new ReadOnlySpan<byte>(Buffer, offset, channelByteLength);
        }
    }

    public class CaptureBufferManager : ICaptureBufferManagerReader, ICaptureBufferManagerWriter
    {
        private readonly Lock readLock = new();

        private readonly ILogger logger;
        private readonly BlockingCollection<CaptureBuffer> availableBuffers;
        private readonly BlockingCollection<CaptureBuffer> filledBuffers;
        private readonly long maxMemoryLimitBytes;

        private int ManagerConfiguration = 1;

        private bool writeInProgress;
        private CaptureBuffer? currentWriteBuffer;
        private bool readInProgress;
        private CaptureBuffer? currentReadBuffer;

        private int maxCaptureCount;

        // Stats (similar to CaptureCircularBuffer)
        private long captureWrites;
        private long captureDrops;
        private long captureReads;
        private long intervalCaptureWrites;
        private long intervalCaptureDrops;
        private long intervalCaptureReads;

        private double cachedCapturesPerSec;
        private DateTimeOffset internalIntervalCaptureStartUtc = DateTimeOffset.UtcNow;
        private long internalIntervalCaptureWrites;

        //public int ChannelCount { get { return channelCount; } }

        public long MaxCaptureCount { get { return maxCaptureCount; } }
        public long CurrentCaptureCount { get { lock (readLock) { return filledBuffers.Count; } } }
        public long CaptureWrites { get { return captureWrites; } }
        public long CaptureDrops { get { return captureDrops; } }
        public long CaptureReads { get { return captureReads; } }
        public long IntervalCaptureWrites { get { return intervalCaptureWrites; } }
        public long IntervalCaptureDrops { get { return intervalCaptureDrops; } }
        public long IntervalCaptureReads { get { return intervalCaptureReads; } }

        public CaptureBufferManager(ILogger logger, long maxMemoryLimitBytes)
        {
            this.logger = logger;
            availableBuffers = new BlockingCollection<CaptureBuffer>();
            filledBuffers = new BlockingCollection<CaptureBuffer>();
            this.maxMemoryLimitBytes = maxMemoryLimitBytes;
        }

        public void Configure(int channelCount, int channelCaptureLength, ThunderscopeDataType captureDataType)
        {
            if (channelCount < 1)
                channelCount = 1;
            if (channelCaptureLength < 1000)
                channelCaptureLength = 1000;
            if (writeInProgress || currentWriteBuffer != null)
                throw new InvalidOperationException("Cannot configure while a write is in progress.");

            int byteWidth = captureDataType.ByteWidth();
            if (byteWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(captureDataType));

            long potentialTotalCaptureLength = (long)channelCaptureLength * channelCount;
            long potentialTotalCaptureLengthBytes = potentialTotalCaptureLength * byteWidth;

            if (potentialTotalCaptureLengthBytes > maxMemoryLimitBytes)
                throw new ArgumentOutOfRangeException("Requested configuration exceeds max memory limit.");

            lock (readLock)
            {
                while (availableBuffers.TryTake(out var entry))
                {
                    ArrayPool<byte>.Shared.Return(entry.Buffer);
                }
                while (filledBuffers.TryTake(out var entry))
                {
                    ArrayPool<byte>.Shared.Return(entry.Buffer);
                }

                var captureByteLength = checked((int)potentialTotalCaptureLengthBytes);
                maxCaptureCount = (int)(maxMemoryLimitBytes / potentialTotalCaptureLengthBytes);

                int uiWaveformDisplayRate = 30;
                if (maxCaptureCount > uiWaveformDisplayRate)
                {
                    maxCaptureCount = uiWaveformDisplayRate;
                    logger.LogDebug($"CaptureBuffer maxCaptureCount coerced to {maxCaptureCount}");
                }

                ManagerConfiguration++;

                for (int i = 0; i < maxCaptureCount; i++)
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(captureByteLength);
                    availableBuffers.Add(new CaptureBuffer(ManagerConfiguration, channelCount, channelCaptureLength, captureDataType, buffer));
                }

                captureWrites = 0;
                captureDrops = 0;
                captureReads = 0;

                intervalCaptureWrites = 0;
                intervalCaptureDrops = 0;
                intervalCaptureReads = 0;

                cachedCapturesPerSec = 0;
                internalIntervalCaptureStartUtc = DateTimeOffset.UtcNow;
                internalIntervalCaptureWrites = 0;
            }
        }

        public bool TryStartWrite(out CaptureBuffer? buffer)
        {
            // TryStartWrite happens on the same thread as Configure, so no need for a lock
            if (writeInProgress || currentWriteBuffer != null)
                throw new InvalidOperationException();

            // Try to take an available buffer; if none, drop oldest filled buffer
            if (availableBuffers.TryTake(out var writeEntry))
            {
                buffer = writeEntry;
                writeInProgress = true;
                currentWriteBuffer = writeEntry;
                return true;
            }
            else if (filledBuffers.TryTake(out var droppedEntry))
            {
                captureDrops++;
                intervalCaptureDrops++;

                buffer = droppedEntry;
                writeInProgress = true;
                currentWriteBuffer = droppedEntry;
                return true;
            }
            else
            {
                buffer = null;
                return false;
            }
        }

        public void FinishWrite(CaptureMetadata captureMetadata)
        {
            // FinishWrite happens on the same thread as Configure, so no need for a lock
            if (!writeInProgress || currentWriteBuffer == null)
                throw new InvalidOperationException();

            // cachedInternalCaptureRatePerSecond logic, used for metadata
            var now = DateTimeOffset.UtcNow;
            var duration = now.Subtract(internalIntervalCaptureStartUtc).TotalSeconds;
            if (duration > 1)
            {
                cachedCapturesPerSec = internalIntervalCaptureWrites / duration;
                internalIntervalCaptureStartUtc = now;
                internalIntervalCaptureWrites = 0;
            }
            internalIntervalCaptureWrites++;

            captureWrites++;
            intervalCaptureWrites++;

            captureMetadata.CapturesPerSec = cachedCapturesPerSec;
            currentWriteBuffer.Metadata = captureMetadata;

            filledBuffers.Add(currentWriteBuffer);
            writeInProgress = false;
            currentWriteBuffer = null;
        }

        public bool TryStartRead(out CaptureBuffer? buffer)
        {
            if (readInProgress || currentReadBuffer != null)
                throw new InvalidOperationException();

            lock (readLock)
            {
                if (!filledBuffers.TryTake(out var filledBuffer))
                {
                    buffer = null;
                    return false;
                }
                buffer = filledBuffer;
                readInProgress = true;
                currentReadBuffer = filledBuffer;
                return true;
            }
        }

        public void FinishRead()
        {
            if (!readInProgress || currentReadBuffer == null)
                throw new InvalidOperationException();

            lock (readLock)
            {
                // If the currentReadBuffer is from a different configuration, discard it
                if (currentReadBuffer.ManagerConfiguration != ManagerConfiguration)
                {
                    ArrayPool<byte>.Shared.Return(currentReadBuffer.Buffer);
                }
                else
                {
                    captureReads++;
                    intervalCaptureReads++;
                    availableBuffers.Add(currentReadBuffer);
                }
                readInProgress = false;
                currentReadBuffer = null;
            }
        }

        public void ResetIntervalStats()
        {
            intervalCaptureWrites = 0;
            intervalCaptureDrops = 0;
            intervalCaptureReads = 0;
        }
    }
}
