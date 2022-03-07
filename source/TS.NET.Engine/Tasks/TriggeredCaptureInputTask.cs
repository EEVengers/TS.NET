using TS.NET.Engine;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Intrinsics.X86;
using Cloudtoid.Interprocess;
using System.Diagnostics;

namespace TS.NET.Engine
{
    public struct TriggeredCapture
    {
        public SemaphoreSlim Semaphore { get; private set; } = new SemaphoreSlim(1);
        public Channels Channels { get; set; }
        public int ChannelLength { get; set; }
        public Memory<byte> ChannelData { get; set; }
    }

    public class TriggeredCaptureInputTask
    {
        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        //, Action<Memory<double>> action
        public void Start(ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger("InputTask");
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(loggerFactory, logger, 8000000, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        // The job of this task - pull data from scope driver/simulator, shuffle if 2/4 channels, horizontal sum, trigger, and produce window segments.
        private static void Loop(ILoggerFactory loggerFactory, ILogger logger, int readChunkLength, CancellationToken cancelToken)
        {
            try
            {
                // This is an inter-process high-performance queue using memory-mapped file & semaphore. Used here to get data from the simulator or hardware.
                var queueFactory = new QueueFactory();
                var queueOptions = new QueueOptions(queueName: "ThunderScope", bytesCapacity: 4 * readChunkLength);
                using var hardwareInput = queueFactory.CreateSubscriber(queueOptions);

                // Configuration values to be updated during runtime
                Channels channels = Channels.Four;
                TriggerChannel triggerChannel = TriggerChannel.One;
                TriggerMode triggerMode = TriggerMode.Normal;
                BoxcarLength boxcarLength = BoxcarLength.None;

                // Various buffers allocated once and reused forevermore.
                Memory<byte> hardwareBuffer = new byte[readChunkLength];
                // Shuffle buffers. Only needed for 2/4 channel modes.
                Span<byte> shuffleBuffer = new byte[readChunkLength];
                // --2 channel buffers
                int channelLength_2 = readChunkLength / 2;
                Span<byte> postShuffleCh1_2 = shuffleBuffer.Slice(0, channelLength_2);
                Span<byte> postShuffleCh2_2 = shuffleBuffer.Slice(channelLength_2, channelLength_2);
                // --4 channel buffers
                int channelLength_4 = readChunkLength / 4;
                Span<byte> postShuffleCh1_4 = shuffleBuffer.Slice(0, channelLength_4);
                Span<byte> postShuffleCh2_4 = shuffleBuffer.Slice(channelLength_4, channelLength_4);
                Span<byte> postShuffleCh3_4 = shuffleBuffer.Slice(channelLength_4 * 2, channelLength_4);
                Span<byte> postShuffleCh4_4 = shuffleBuffer.Slice(channelLength_4 * 3, channelLength_4);

                Span<ulong> triggerResultBuffer_1 = new ulong[readChunkLength / 64];
                Span<ulong> triggerResultBuffer_2 = new ulong[readChunkLength / 2 / 64];
                Span<ulong> triggerResultBuffer_4 = new ulong[readChunkLength / 4 / 64];
                ulong holdoffSamples = 1000;
                RisingEdgeTrigger trigger = new(200, 190, holdoffSamples);

                //TriggeredCapture triggeredCapture = new() { Channels = Channels.Four, ChannelLength = acquisitionLength, ChannelData = new byte[acquisitionLength * 4] };
                DateTimeOffset startTime = DateTimeOffset.UtcNow;

                int dequeueCounter = 0;
                uint triggerCount = 0;
                uint oneSecondTriggerCount = 0;
                uint totalTriggerCount = 0;

                int bufferCapacity = Math.Max(readChunkLength, 10 * 1000000);       // The larger of either readChunkLength or the configured buffer length

                // Postbox is cross-process shared memory for the UI to read triggered acquisitions
                // The trigger point is _always_ in the middle of the buffer, and when the UI sets positive/negative trigger point, it's just moving the UI viewport
                Postbox ch1 = new(new PostboxOptions("ThunderScopeCh1", bufferCapacity), loggerFactory);            
                Postbox ch2 = new(new PostboxOptions("ThunderScopeCh2", bufferCapacity), loggerFactory);
                Postbox ch3 = new(new PostboxOptions("ThunderScopeCh3", bufferCapacity), loggerFactory);
                Postbox ch4 = new(new PostboxOptions("ThunderScopeCh4", bufferCapacity), loggerFactory);

                int droppedTriggersCh1 = 0;
                int droppedTriggersCh2 = 0;
                int droppedTriggersCh3 = 0;
                int droppedTriggersCh4 = 0;

                Stopwatch oneSecond = Stopwatch.StartNew();

                unsafe
                {
                    byte[] circularBufferBackingStoreCh1 = new byte[bufferCapacity];
                    byte[] circularBufferBackingStoreCh2 = new byte[bufferCapacity];
                    byte[] circularBufferBackingStoreCh3 = new byte[bufferCapacity];
                    byte[] circularBufferBackingStoreCh4 = new byte[bufferCapacity];
                    fixed (byte*
                        circularBufferBackingStoreCh1Ptr = circularBufferBackingStoreCh1,
                        circularBufferBackingStoreCh2Ptr = circularBufferBackingStoreCh2,
                        circularBufferBackingStoreCh3Ptr = circularBufferBackingStoreCh3,
                        circularBufferBackingStoreCh4Ptr = circularBufferBackingStoreCh4)
                    {
                        var circularBuffer1 = new CircularBuffer(circularBufferBackingStoreCh1Ptr, bufferCapacity);
                        var circularBuffer2 = new CircularBuffer(circularBufferBackingStoreCh2Ptr, bufferCapacity);
                        var circularBuffer3 = new CircularBuffer(circularBufferBackingStoreCh3Ptr, bufferCapacity);
                        var circularBuffer4 = new CircularBuffer(circularBufferBackingStoreCh4Ptr, bufferCapacity);

                        while (true)
                        {
                            cancelToken.ThrowIfCancellationRequested();
                            var inputBuffer = hardwareInput.Dequeue(hardwareBuffer, cancelToken);
                            // Add a zero-wait mechanism here that allows for configuration values to be updated
                            dequeueCounter++;
                            switch (channels)
                            {
                                // Processing pipeline:
                                // Shuffle (if needed)
                                // Boxcar
                                // Write to circular buffer
                                // Trigger
                                // Data segment on trigger (if needed)
                                case Channels.None:
                                    break;
                                case Channels.One:
                                    // Boxcar
                                    if (boxcarLength != BoxcarLength.None)
                                        throw new NotImplementedException();
                                    // Write to circular buffer
                                    circularBuffer1.Write(inputBuffer.Span, 0);
                                    // Trigger
                                    if (triggerChannel == TriggerChannel.One)
                                        triggerCount = trigger.ProcessSimd(input: inputBuffer.Span, trigger: triggerResultBuffer_1);
                                    break;
                                case Channels.Two:
                                    // Shuffle
                                    Shuffle.TwoChannels(input: inputBuffer.Span, output: shuffleBuffer);
                                    // Boxcar
                                    if (boxcarLength != BoxcarLength.None)
                                        throw new NotImplementedException();
                                    // Write to circular buffer
                                    circularBuffer1.Write(postShuffleCh1_2, 0);
                                    circularBuffer2.Write(postShuffleCh2_2, 0);
                                    // Trigger
                                    if (triggerChannel != TriggerChannel.None)
                                    {
                                        var triggerChannelBuffer = triggerChannel switch
                                        {
                                            TriggerChannel.One => postShuffleCh1_2,
                                            TriggerChannel.Two => postShuffleCh2_2,
                                            _ => throw new ArgumentException("Invalid TriggerChannel value")
                                        };
                                        triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, trigger: triggerResultBuffer_2);
                                    }
                                    break;
                                case Channels.Four:
                                    // Shuffle
                                    Shuffle.FourChannels(input: inputBuffer.Span, output: shuffleBuffer);
                                    // Boxcar
                                    if (boxcarLength != BoxcarLength.None)
                                        throw new NotImplementedException();
                                    // Write to circular buffer
                                    circularBuffer1.Write(postShuffleCh1_4, 0);
                                    circularBuffer2.Write(postShuffleCh2_4, 0);
                                    circularBuffer3.Write(postShuffleCh3_4, 0);
                                    circularBuffer4.Write(postShuffleCh4_4, 0);
                                    // Trigger
                                    if (triggerChannel != TriggerChannel.None)
                                    {
                                        var triggerChannelBuffer = triggerChannel switch
                                        {
                                            TriggerChannel.One => postShuffleCh1_4,
                                            TriggerChannel.Two => postShuffleCh2_4,
                                            TriggerChannel.Three => postShuffleCh3_4,
                                            TriggerChannel.Four => postShuffleCh4_4,
                                            _ => throw new ArgumentException("Invalid TriggerChannel value")
                                        };
                                        triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, trigger: triggerResultBuffer_1);
                                        totalTriggerCount += triggerCount;
                                        oneSecondTriggerCount += triggerCount;
                                        if (triggerCount > 0)
                                        {
                                            if (oneSecond.ElapsedMilliseconds >= 1000)
                                            {
                                                Console.WriteLine($"Triggers/sec: {oneSecondTriggerCount / (oneSecond.ElapsedMilliseconds * 0.001):F2}, dequeue count: {dequeueCounter}, trigger count: {totalTriggerCount}");
                                                oneSecond.Restart();
                                                oneSecondTriggerCount = 0;
                                            }

                                            // Scan through trigger buffer to find first trigger bit
                                            for (int i = 0; i < triggerResultBuffer_1.Length; i++)
                                            {
                                                // To do: need to store pre-trigger data so it can be prepended?
                                                if (triggerResultBuffer_1[i] > 0)
                                                {
                                                    var index = (i * 64) + (64 - (int)Lzcnt.X64.LeadingZeroCount(triggerResultBuffer_1[i])) - 1;

                                                    if (ch1.IsReadyToWrite())
                                                    {
                                                        circularBuffer1.Read(0, bufferCapacity, ch1.DataPointer);
                                                        ch1.DataIsWritten();
                                                    }
                                                    else
                                                    {
                                                        droppedTriggersCh1++;
                                                    }

                                                    if (ch2.IsReadyToWrite())
                                                    {
                                                        circularBuffer2.Read(0, bufferCapacity, ch2.DataPointer);
                                                        ch2.DataIsWritten();
                                                    }
                                                    else
                                                    {
                                                        droppedTriggersCh2++;
                                                    }

                                                    if (ch3.IsReadyToWrite())
                                                    {
                                                        circularBuffer3.Read(0, bufferCapacity, ch3.DataPointer);
                                                        ch3.DataIsWritten();
                                                    }
                                                    else
                                                    {
                                                        droppedTriggersCh3++;
                                                    }

                                                    if (ch4.IsReadyToWrite())
                                                    {
                                                        circularBuffer4.Read(0, bufferCapacity, ch4.DataPointer);
                                                        ch4.DataIsWritten();
                                                    }
                                                    else
                                                    {
                                                        droppedTriggersCh4++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}, Ch3 triggers: {triggerCount3}, Ch4 triggers: {triggerCount4} ");
                                    break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(TriggeredCaptureInputTask)} stopping");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(TriggeredCaptureInputTask)} error");
                throw;
            }
            finally
            {
                logger.LogDebug($"{nameof(TriggeredCaptureInputTask)} stopped");
            }
        }
    }
}
