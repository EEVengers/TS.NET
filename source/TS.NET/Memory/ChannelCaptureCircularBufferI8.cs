namespace TS.NET
{
    // Windows are copied out the ChannelSampleCircularBufferI8 into this ChannelCaptureCircularBufferI8 when triggered, streamed or forced.
    // This allows time for UI consumers to request multiple captures, particularly if the capture length is short (e.g. 1000 samples at ~1M captures/sec).
    // When the user changes the capture length, this buffer will empty, and then configure to allow the maximum number of captures in the given memory.
    // When the user changes various settings, this buffer will empty.
    //
    // Needs to be thread safe for a single writer and a single reader.
    // The MMF becomes a pure data exchange mechanism
    //
    // IMPORTANT: only a single reader/writer allowed otherwise data corruption will result
    public class ChannelCaptureCircularBufferI8 : IDisposable
    {
        private readonly NativeMemoryAligned<sbyte> buffer;

        private object configurationLock = new();

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

        private ThunderscopeHardwareConfig hardwareConfig;
        private ThunderscopeProcessingConfig processingConfig;

        Dictionary<long, bool> triggered;
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

        public ChannelCaptureCircularBufferI8(long totalBufferLength)
        {
            buffer = new NativeMemoryAligned<sbyte>(totalBufferLength);
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

            lock (configurationLock)
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
            lock (configurationLock)
            {
                currentCaptureCount = 0;
                captureTotal = 0;
                captureDrops = 0;
                captureReads = 0;

                writeCaptureOffset = 0;
                readCaptureOffset = 0;
                triggered = [];
            }
        }

        public bool TryStartWrite()
        {
            if (writeInProgress)
                throw new InvalidOperationException();

            lock (configurationLock)
            {
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
        }

        public void FinishWrite(bool triggered, ThunderscopeHardwareConfig hardwareConfig, ThunderscopeProcessingConfig processingConfig)
        {
            lock (configurationLock)
            {
                this.triggered[writeCaptureOffset] = triggered;
                this.hardwareConfig = hardwareConfig;
                this.processingConfig = processingConfig;

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

        public bool TryStartRead(out bool triggered, out ThunderscopeHardwareConfig hardwareConfig, out ThunderscopeProcessingConfig processingConfig)
        {
            if (readInProgress)
                throw new InvalidOperationException();

            lock (configurationLock)
            {
                if (currentCaptureCount > 0)
                {
                    triggered = this.triggered[readCaptureOffset];
                    hardwareConfig = this.hardwareConfig;
                    processingConfig = this.processingConfig;
                    readInProgress = true;
                    return true;
                }
                else
                {
                    triggered = false;
                    hardwareConfig = default;
                    processingConfig = default;
                    return false;
                }
            }
        }

        public void FinishRead()
        {
            lock (configurationLock)
            {
                currentCaptureCount--;
                readCaptureOffset += captureLengthBytes;
                if (readCaptureOffset >= wraparoundOffset)
                    readCaptureOffset = 0;
                captureReads++;
                intervalCaptureReads++;
                readInProgress = false;
            }
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
            lock (configurationLock)
            {
                intervalCaptureTotal = 0;
                intervalCaptureDrops = 0;
                intervalCaptureReads = 0;
            }
        }
    }
}



//namespace TS.NET
//{
//    // Windows are copied out the ChannelSampleCircularBufferI8 into this ChannelCaptureCircularBufferI8 when triggered, streamed or forced.
//    // This allows time for UI consumers to request multiple captures, particularly if the capture length is short (e.g. 1000 samples at ~1M captures/sec).
//    // When the user changes the capture length, this buffer will empty, and then configure to allow the maximum number of captures in the given memory.
//    //
//    // Needs to be thread safe for a single writer and a single reader.
//    // The MMF becomes a pure data exchange mechanism.
//    public class ChannelCaptureCircularBufferI8 : IDisposable
//    {
//        private readonly NativeMemoryAligned<sbyte> buffer;

//        // Use simple locking for now, but maybe fine grained locks later to allow simultaneous read/write memory operations (to different regions of the buffer)
//        //private object overallLock = new();
//        //private object readLock;
//        //private object writeLock;

//        private long captureLength;
//        private int channelCount;
//        private int channelLength;

//        private long maxCaptureCount;
//        private int currentCaptureCount;
//        private int capturesSaved;
//        private int capturesDropped;

//        private long writeCaptureOffset;
//        private long readCaptureOffset;
//        private long wraparoundOffset;

//        public int ChannelCount {  get { return channelCount; } }
//        public long CapturesSaved { get { return capturesSaved; } }
//        public long CapturesDropped { get { return capturesDropped; } }
//        public long MaxCaptureCount { get { return maxCaptureCount; } }

//        public ChannelCaptureCircularBufferI8(long totalBufferLength)
//        {
//            buffer = new NativeMemoryAligned<sbyte>(totalBufferLength);
//        }

//        public void Dispose()
//        {
//            buffer.Dispose();
//            GC.SuppressFinalize(this);
//        }

//        public void Configure(int channelLength, int channelCount)
//        {
//            var captureLength = (long)channelLength * channelCount;
//            if (channelLength < 100) throw new ArgumentOutOfRangeException($"{nameof(channelLength)} cannot be less than 100.");
//            if (captureLength > buffer.Length) throw new ArgumentOutOfRangeException($"Requested configuration exceeds memory size.");

//            lock (this)
//            {
//                this.captureLength = captureLength;
//                this.channelCount = channelCount;
//                this.channelLength = channelLength;
//                maxCaptureCount = buffer.Length / captureLength;       // This will be a value from 1 to long.MaxValue
//                currentCaptureCount = 0;
//                writeCaptureOffset = 0;
//                readCaptureOffset = 0;
//                wraparoundOffset = maxCaptureCount * captureLength;
//                capturesSaved = 0;
//                capturesDropped = 0;
//            }
//        }

//        public bool TryGetWriteCaptureBuffer4Ch(out Span<sbyte> writeCaptureBuffer1, out Span<sbyte> writeCaptureBuffer2, out Span<sbyte> writeCaptureBuffer3, out Span<sbyte> writeCaptureBuffer4)
//        {
//            lock (this)
//            {
//                if (channelCount != 4)
//                    throw new Exception("4 channel method used when channelCount != 4");
//                if (currentCaptureCount < maxCaptureCount)
//                {
//                    writeCaptureBuffer1 = buffer.AsSpan(writeCaptureOffset, channelLength);
//                    writeCaptureBuffer2 = buffer.AsSpan(writeCaptureOffset + channelLength, channelLength);
//                    writeCaptureBuffer3 = buffer.AsSpan(writeCaptureOffset + channelLength + channelLength, channelLength);
//                    writeCaptureBuffer4 = buffer.AsSpan(writeCaptureOffset + channelLength + channelLength + channelLength, channelLength);
//                    currentCaptureCount++;
//                    writeCaptureOffset += captureLength;
//                    if (writeCaptureOffset >= wraparoundOffset)
//                        writeCaptureOffset = 0;

//                    capturesSaved++;
//                    return true;
//                }
//                else
//                {
//                    writeCaptureBuffer1 = null;
//                    writeCaptureBuffer2 = null;
//                    writeCaptureBuffer3 = null;
//                    writeCaptureBuffer4 = null;
//                    capturesDropped++;
//                    return false;
//                }
//            }
//        }

//        public bool TryGetWriteCaptureBuffer2Ch(out Span<sbyte> writeCaptureBuffer1, out Span<sbyte> writeCaptureBuffer2)
//        {
//            lock (this)
//            {
//                if (channelCount != 2)
//                    throw new Exception("2 channel method used when channelCount != 2");
//                if (currentCaptureCount < maxCaptureCount)
//                {
//                    writeCaptureBuffer1 = buffer.AsSpan(writeCaptureOffset, channelLength);
//                    writeCaptureBuffer2 = buffer.AsSpan(writeCaptureOffset + channelLength, channelLength);
//                    currentCaptureCount++;
//                    writeCaptureOffset += captureLength;
//                    if (writeCaptureOffset >= wraparoundOffset)
//                        writeCaptureOffset = 0;

//                    capturesSaved++;
//                    return true;
//                }
//                else
//                {
//                    writeCaptureBuffer1 = null;
//                    writeCaptureBuffer2 = null;
//                    capturesDropped++;
//                    return false;
//                }
//            }
//        }

//        public bool TryGetWriteCaptureBuffer1Ch(out Span<sbyte> writeCaptureBuffer1)
//        {
//            lock (this)
//            {
//                if (channelCount != 1)
//                    throw new Exception("1 channel method used when channelCount != 1");
//                if (currentCaptureCount < maxCaptureCount)
//                {
//                    writeCaptureBuffer1 = buffer.AsSpan(writeCaptureOffset, channelLength);
//                    currentCaptureCount++;
//                    writeCaptureOffset += captureLength;
//                    if (writeCaptureOffset >= wraparoundOffset)
//                        writeCaptureOffset = 0;

//                    capturesSaved++;
//                    return true;
//                }
//                else
//                {
//                    writeCaptureBuffer1 = null;
//                    capturesDropped++;
//                    return false;
//                }
//            }
//        }

//        public bool TryGetReadCaptureBuffer1Ch(out ReadOnlySpan<sbyte> readCaptureBuffer1)
//        {
//            lock (this)
//            {
//                if (currentCaptureCount > 0)
//                {
//                    readCaptureBuffer1 = buffer.AsSpan(readCaptureOffset, channelLength);
//                    currentCaptureCount--;
//                    readCaptureOffset += captureLength;
//                    if (readCaptureOffset >= wraparoundOffset)
//                        readCaptureOffset = 0;
//                    return true;
//                }
//                else
//                {
//                    readCaptureBuffer1 = null;
//                    return false;
//                }
//            }
//        }

//        public bool TryGetReadCaptureBuffer2Ch(out ReadOnlySpan<sbyte> readCaptureBuffer1, out ReadOnlySpan<sbyte> readCaptureBuffer2)
//        {
//            lock (this)
//            {
//                if (currentCaptureCount > 0)
//                {
//                    readCaptureBuffer1 = buffer.AsSpan(readCaptureOffset, channelLength);
//                    readCaptureBuffer2 = buffer.AsSpan(readCaptureOffset + channelLength, channelLength);
//                    currentCaptureCount--;
//                    readCaptureOffset += captureLength;
//                    if (readCaptureOffset >= wraparoundOffset)
//                        readCaptureOffset = 0;
//                    return true;
//                }
//                else
//                {
//                    readCaptureBuffer1 = null;
//                    readCaptureBuffer2 = null;
//                    return false;
//                }
//            }
//        }

//        public bool TryGetReadCaptureBuffer4Ch(out ReadOnlySpan<sbyte> readCaptureBuffer1, out ReadOnlySpan<sbyte> readCaptureBuffer2, out ReadOnlySpan<sbyte> readCaptureBuffer3, out ReadOnlySpan<sbyte> readCaptureBuffer4)
//        {
//            lock (this)
//            {
//                if (currentCaptureCount > 0)
//                {
//                    readCaptureBuffer1 = buffer.AsSpan(readCaptureOffset, channelLength);
//                    readCaptureBuffer2 = buffer.AsSpan(readCaptureOffset + channelLength, channelLength);
//                    readCaptureBuffer3 = buffer.AsSpan(readCaptureOffset + channelLength + channelLength, channelLength);
//                    readCaptureBuffer4 = buffer.AsSpan(readCaptureOffset + channelLength + channelLength + channelLength, channelLength);
//                    currentCaptureCount--;
//                    readCaptureOffset += captureLength;
//                    if (readCaptureOffset >= wraparoundOffset)
//                        readCaptureOffset = 0;
//                    return true;
//                }
//                else
//                {
//                    readCaptureBuffer1 = null;
//                    readCaptureBuffer2 = null;
//                    readCaptureBuffer3 = null;
//                    readCaptureBuffer4 = null;
//                    return false;
//                }
//            }
//        }
//    }
//}
