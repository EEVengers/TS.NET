using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace TS.NET.Engine
{
    public class ProcessingTask
    {
        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        //, Action<Memory<double>> action
        public void Start(ILoggerFactory loggerFactory, BlockingChannelReader<ThunderscopeMemory> processingPool, BlockingChannelWriter<ThunderscopeMemory> memoryPool)
        {
            var logger = loggerFactory.CreateLogger("ProcessingTask");
            cancelTokenSource = new CancellationTokenSource();
            int bufferLength = 100 * 1000 * 1000;
            // Postbox is cross-process shared memory for the UI to read triggered acquisitions
            // The trigger point is _always_ in the middle of the channel block, and when the UI sets positive/negative trigger point, it's just moving the UI viewport
            Postbox postbox = new(new PostboxOptions("ThunderScope.1", bufferLength), loggerFactory);
            taskLoop = Task.Factory.StartNew(() => Loop(logger, processingPool, memoryPool, postbox, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        // The job of this task - pull data from scope driver/simulator, shuffle if 2/4 channels, horizontal sum, trigger, and produce window segments.
        private static void Loop(ILogger logger, BlockingChannelReader<ThunderscopeMemory> processingPool, BlockingChannelWriter<ThunderscopeMemory> memoryPool, Postbox postbox, CancellationToken cancelToken)
        {
            try
            {
                Thread.CurrentThread.Name = "TS.NET Processing";

                // Configuration values to be updated during runtime
                Channels channels = Channels.Four;
                TriggerChannel triggerChannel = TriggerChannel.One;
                TriggerMode triggerMode = TriggerMode.Normal;
                BoxcarLength boxcarLength = BoxcarLength.None;

                // Various buffers allocated once and reused forevermore.
                Memory<byte> hardwareBuffer = new byte[ThunderscopeMemory.Length];
                // Shuffle buffers. Only needed for 2/4 channel modes.
                Span<byte> shuffleBuffer = new byte[ThunderscopeMemory.Length];
                // --2 channel buffers
                int channelLength_2 = (int)ThunderscopeMemory.Length / 2;
                Span<byte> postShuffleCh1_2 = shuffleBuffer.Slice(0, channelLength_2);
                Span<byte> postShuffleCh2_2 = shuffleBuffer.Slice(channelLength_2, channelLength_2);
                // --4 channel buffers
                int channelLength_4 = (int)ThunderscopeMemory.Length / 4;
                Span<byte> postShuffleCh1_4 = shuffleBuffer.Slice(0, channelLength_4);
                Span<byte> postShuffleCh2_4 = shuffleBuffer.Slice(channelLength_4, channelLength_4);
                Span<byte> postShuffleCh3_4 = shuffleBuffer.Slice(channelLength_4 * 2, channelLength_4);
                Span<byte> postShuffleCh4_4 = shuffleBuffer.Slice(channelLength_4 * 3, channelLength_4);

                Span<ulong> triggerResultBuffer_1 = new ulong[ThunderscopeMemory.Length / 64];
                Span<ulong> triggerResultBuffer_2 = new ulong[ThunderscopeMemory.Length / 2 / 64];
                Span<ulong> triggerResultBuffer_4 = new ulong[ThunderscopeMemory.Length / 4 / 64];
                ulong holdoffSamples = 1000;
                RisingEdgeTrigger trigger = new(200, 190, holdoffSamples);

                //TriggeredCapture triggeredCapture = new() { Channels = Channels.Four, ChannelLength = acquisitionLength, ChannelData = new byte[acquisitionLength * 4] };
                DateTimeOffset startTime = DateTimeOffset.UtcNow;

                int dequeueCounter = 0;
                uint triggerCount = 0;
                uint oneSecondTriggerCount = 0;
                uint totalTriggerCount = 0;

                // channelLength = the larger of:
                //     ThunderscopeMemory.Length divided by the number of channels
                //     The buffer length divided by the number of channels
                // BoxcarUtility.ToDivisor(boxcarLength)
                long channelLength = Math.Max(ThunderscopeMemory.Length / (long)channels, postbox.BytesCapacity / (long)channels);
                var postboxWriterSemaphore = postbox.GetWriterSemaphore();
                int droppedTriggers = 0;

                Stopwatch oneSecond = Stopwatch.StartNew();

                unsafe
                {
                    byte[] circularBufferBackingStoreCh1 = new byte[channelLength];
                    byte[] circularBufferBackingStoreCh2 = new byte[channelLength];
                    byte[] circularBufferBackingStoreCh3 = new byte[channelLength];
                    byte[] circularBufferBackingStoreCh4 = new byte[channelLength];
                    fixed (byte*
                        circularBufferBackingStoreCh1Ptr = circularBufferBackingStoreCh1,
                        circularBufferBackingStoreCh2Ptr = circularBufferBackingStoreCh2,
                        circularBufferBackingStoreCh3Ptr = circularBufferBackingStoreCh3,
                        circularBufferBackingStoreCh4Ptr = circularBufferBackingStoreCh4)
                    {
                        var circularBuffer1 = new CircularBuffer(circularBufferBackingStoreCh1Ptr, channelLength);
                        var circularBuffer2 = new CircularBuffer(circularBufferBackingStoreCh2Ptr, channelLength);
                        var circularBuffer3 = new CircularBuffer(circularBufferBackingStoreCh3Ptr, channelLength);
                        var circularBuffer4 = new CircularBuffer(circularBufferBackingStoreCh4Ptr, channelLength);

                        while (true)
                        {
                            cancelToken.ThrowIfCancellationRequested();
                            var memory = processingPool.Read(cancelToken);
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
                                    circularBuffer1.Write(memory.Span, 0);
                                    // Trigger
                                    if (triggerChannel != TriggerChannel.None)
                                    {
                                        var triggerChannelBuffer = triggerChannel switch
                                        {
                                            TriggerChannel.One => memory.Span,
                                            _ => throw new ArgumentException("Invalid TriggerChannel value")
                                        };
                                        triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, trigger: triggerResultBuffer_1);
                                    }
                                    // Finished with the memory, return it
                                    memoryPool.Write(memory);
                                    break;
                                case Channels.Two:
                                    // Shuffle
                                    Shuffle.TwoChannels(input: memory.Span, output: shuffleBuffer);
                                    // Finished with the memory, return it
                                    memoryPool.Write(memory);
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
                                    Shuffle.FourChannels(input: memory.Span, output: shuffleBuffer);
                                    // Finished with the memory, return it
                                    memoryPool.Write(memory);
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
                                            // Scan through trigger buffer to find first trigger bit
                                            for (int i = 0; i < triggerResultBuffer_1.Length; i++)
                                            {
                                                // To do: need to store pre-trigger data so it can be prepended?
                                                if (triggerResultBuffer_1[i] > 0)
                                                {
                                                    //var index = (i * 64) + (64 - (int)Lzcnt.X64.LeadingZeroCount(triggerResultBuffer_1[i])) - 1;

                                                    if (postbox.IsReadyToWrite())
                                                    {
                                                        circularBuffer1.Read(0, channelLength, postbox.DataPointer);
                                                        circularBuffer2.Read(0, channelLength, postbox.DataPointer + channelLength);
                                                        circularBuffer3.Read(0, channelLength, postbox.DataPointer + channelLength + channelLength);
                                                        circularBuffer4.Read(0, channelLength, postbox.DataPointer + channelLength + channelLength + channelLength);
                                                        postbox.DataIsWritten();
                                                        postboxWriterSemaphore.Release();       // Signal to the reader that data is available
                                                    }
                                                    else
                                                    {
                                                        droppedTriggers++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}, Ch3 triggers: {triggerCount3}, Ch4 triggers: {triggerCount4} ");
                                    break;
                            }


                            if (oneSecond.ElapsedMilliseconds >= 1000)
                            {
                                logger.LogDebug($"Triggers/sec: {oneSecondTriggerCount / (oneSecond.ElapsedMilliseconds * 0.001):F2}, dequeue count: {dequeueCounter}, trigger count: {totalTriggerCount}");
                                oneSecond.Restart();
                                oneSecondTriggerCount = 0;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(ProcessingTask)} stopping");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(ProcessingTask)} error");
                throw;
            }
            finally
            {
                logger.LogDebug($"{nameof(ProcessingTask)} stopped");
            }
        }
    }
}
