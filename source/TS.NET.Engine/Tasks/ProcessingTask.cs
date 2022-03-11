using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;

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
            ulong capacity = 4 * 100 * 1000 * 1000;      //Maximum record length = 100M samples per channel
            // Bridge is cross-process shared memory for the UI to read triggered acquisitions
            // The trigger point is _always_ in the middle of the channel block, and when the UI sets positive/negative trigger point, it's just moving the UI viewport
            ThunderscopeBridgeWriter bridge = new(new ThunderscopeBridgeOptions("ThunderScope.1", capacity), loggerFactory);
            taskLoop = Task.Factory.StartNew(() => Loop(logger, processingPool, memoryPool, bridge, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        // The job of this task - pull data from scope driver/simulator, shuffle if 2/4 channels, horizontal sum, trigger, and produce window segments.
        private static void Loop(ILogger logger, BlockingChannelReader<ThunderscopeMemory> processingPool, BlockingChannelWriter<ThunderscopeMemory> memoryPool, ThunderscopeBridgeWriter bridge, CancellationToken cancelToken)
        {
            try
            {
                Thread.CurrentThread.Name = "TS.NET Processing";

                // Configuration values to be updated during runtime... conveiniently all on ThunderscopeMemoryBridgeHeader
                ThunderscopeConfiguration config = new()
                {
                    Channels = Channels.Four,
                    ChannelLength = (ulong)ChannelLength.OneHundredM,
                    BoxcarLength = BoxcarLength.None,
                    TriggerChannel = TriggerChannel.One,
                    TriggerMode = TriggerMode.Normal
                };
                bridge.Configuration = config;

                ThunderscopeMonitoring monitoring = new()
                {
                    TotalTriggers = 0,
                    MissedTriggers = 0
                };
                bridge.Monitoring = monitoring;
                var bridgeWriterSemaphore = bridge.GetWriterSemaphore();

                // Various buffers allocated once and reused forevermore.
                //Memory<byte> hardwareBuffer = new byte[ThunderscopeMemory.Length];
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

                Span<uint> triggerIndices = new uint[ThunderscopeMemory.Length / 1000];     // 1000 samples is the minimum holdoff
                RisingEdgeTrigger trigger = new(200, 190, config.ChannelLength / 2);

                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                uint dequeueCounter = 0;
                uint triggerCount = 0;
                uint oneSecondTriggerCount = 0;
                // BoxcarUtility.ToDivisor(boxcarLength)
                Stopwatch oneSecond = Stopwatch.StartNew();

                var circularBuffer1 = new ChannelCircularAlignedBuffer((uint)config.ChannelLength);
                var circularBuffer2 = new ChannelCircularAlignedBuffer((uint)config.ChannelLength);
                var circularBuffer3 = new ChannelCircularAlignedBuffer((uint)config.ChannelLength);
                var circularBuffer4 = new ChannelCircularAlignedBuffer((uint)config.ChannelLength);

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    var memory = processingPool.Read(cancelToken);
                    // Add a zero-wait mechanism here that allows for configuration values to be updated
                    // (which will require updating many of the intermediate variables/buffers)
                    dequeueCounter++;
                    int channelLength = (int)config.ChannelLength;
                    switch (config.Channels)
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
                            if (config.BoxcarLength != BoxcarLength.None)
                                throw new NotImplementedException();
                            // Write to circular buffer
                            circularBuffer1.Write(memory.Span);
                            // Trigger
                            if (config.TriggerChannel != TriggerChannel.None)
                            {
                                var triggerChannelBuffer = config.TriggerChannel switch
                                {
                                    TriggerChannel.One => memory.Span,
                                    _ => throw new ArgumentException("Invalid TriggerChannel value")
                                };
                                triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, triggerIndices: triggerIndices);
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
                            if (config.BoxcarLength != BoxcarLength.None)
                                throw new NotImplementedException();
                            // Write to circular buffer
                            circularBuffer1.Write(postShuffleCh1_2);
                            circularBuffer2.Write(postShuffleCh2_2);
                            // Trigger
                            if (config.TriggerChannel != TriggerChannel.None)
                            {
                                var triggerChannelBuffer = config.TriggerChannel switch
                                {
                                    TriggerChannel.One => postShuffleCh1_2,
                                    TriggerChannel.Two => postShuffleCh2_2,
                                    _ => throw new ArgumentException("Invalid TriggerChannel value")
                                };
                                triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, triggerIndices: triggerIndices);
                            }
                            break;
                        case Channels.Four:
                            // Shuffle
                            Shuffle.FourChannels(input: memory.Span, output: shuffleBuffer);
                            // Finished with the memory, return it
                            memoryPool.Write(memory);
                            // Boxcar
                            if (config.BoxcarLength != BoxcarLength.None)
                                throw new NotImplementedException();
                            // Write to circular buffer
                            circularBuffer1.Write(postShuffleCh1_4);
                            circularBuffer2.Write(postShuffleCh2_4);
                            circularBuffer3.Write(postShuffleCh3_4);
                            circularBuffer4.Write(postShuffleCh4_4);
                            // Trigger
                            if (config.TriggerChannel != TriggerChannel.None)
                            {
                                var triggerChannelBuffer = config.TriggerChannel switch
                                {
                                    TriggerChannel.One => postShuffleCh1_4,
                                    TriggerChannel.Two => postShuffleCh2_4,
                                    TriggerChannel.Three => postShuffleCh3_4,
                                    TriggerChannel.Four => postShuffleCh4_4,
                                    _ => throw new ArgumentException("Invalid TriggerChannel value")
                                };
                                triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, triggerIndices: triggerIndices);
                                monitoring.TotalTriggers += triggerCount;
                                oneSecondTriggerCount += triggerCount;
                                if (triggerCount > 0)
                                {
                                    for (int i = 0; i < triggerCount; i++)
                                    {
                                        if (bridge.IsReadyToWrite)
                                        {
                                            bridge.Monitoring = monitoring;
                                            var bridgeSpan = bridge.Span;
                                            var triggerIndex = triggerIndices[i];
                                            circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), triggerIndex);
                                            circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), triggerIndex);
                                            circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), triggerIndex);
                                            circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), triggerIndex);
                                            bridge.DataWritten();
                                            bridgeWriterSemaphore.Release();       // Signal to the reader that data is available
                                        }
                                        else
                                        {
                                            monitoring.MissedTriggers++;
                                        }
                                    }
                                }
                            }
                            //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}, Ch3 triggers: {triggerCount3}, Ch4 triggers: {triggerCount4} ");
                            break;
                    }

                    if (oneSecond.ElapsedMilliseconds >= 1000)
                    {
                        logger.LogDebug($"Triggers/sec: {oneSecondTriggerCount / (oneSecond.ElapsedMilliseconds * 0.001):F2}, dequeue count: {dequeueCounter}, trigger count: {monitoring.TotalTriggers}, UI displayed triggers: {monitoring.TotalTriggers - monitoring.MissedTriggers}, UI dropped triggers: {monitoring.MissedTriggers}");
                        oneSecond.Restart();
                        oneSecondTriggerCount = 0;
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
