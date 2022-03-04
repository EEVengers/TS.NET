using Cloudtoid.Interprocess;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Intrinsics.X86;

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
        public void Start(ILoggerFactory loggerFactory, BlockingChannelWriter<TriggeredCapture> triggeredCaptureWriter)
        {
            var logger = loggerFactory.CreateLogger("InputTask");
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, 8000000, triggeredCaptureWriter, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        // The job of this task - pull data from scope driver/simulator, shuffle if 2/4 channels, horizontal sum, trigger, and produce window segments.
        private static void Loop(ILogger logger, int inputLength, BlockingChannelWriter<TriggeredCapture> triggeredCaptureWriter, CancellationToken cancelToken)
        {
            try
            {
                // This is an inter-process high-performance queue using memory-mapped file & semaphore. Used here to get data from the simulator or hardware.
                var queueFactory = new QueueFactory();
                var queueOptions = new QueueOptions(queueName: "ThunderScope", bytesCapacity: 2 * 8000000);
                using var hardwareInput = queueFactory.CreateSubscriber(queueOptions);

                // Configuration values to be updated during runtime
                Channels channels = Channels.Four;
                TriggerChannel triggerChannel = TriggerChannel.One;
                TriggerMode triggerMode = TriggerMode.Normal;
                BoxcarLength boxcarLength = BoxcarLength.None;

                // Various buffers allocated once and reused forevermore.
                Memory<byte> hardwareBuffer = new byte[inputLength];
                // Shuffle buffers. Only needed for 2/4 channel modes.
                Span<byte> shuffleBuffer = new byte[inputLength];
                // --2 channel buffers
                int channelLength_2 = inputLength / 2;
                Span<byte> postShuffleCh1_2 = shuffleBuffer.Slice(0, channelLength_2);
                Span<byte> postShuffleCh2_2 = shuffleBuffer.Slice(channelLength_2, channelLength_2);
                // --4 channel buffers
                int channelLength_4 = inputLength / 4;
                Span<byte> postShuffleCh1_4 = shuffleBuffer.Slice(0, channelLength_4);
                Span<byte> postShuffleCh2_4 = shuffleBuffer.Slice(channelLength_4, channelLength_4);
                Span<byte> postShuffleCh3_4 = shuffleBuffer.Slice(channelLength_4 * 2, channelLength_4);
                Span<byte> postShuffleCh4_4 = shuffleBuffer.Slice(channelLength_4 * 3, channelLength_4);

                //int triggerChannelLength = channelLength / 64;
                Span<ulong> triggerResultBuffer_1 = new ulong[inputLength / 64];
                Span<ulong> triggerResultBuffer_2 = new ulong[inputLength / 2 / 64];
                Span<ulong> triggerResultBuffer_4 = new ulong[inputLength / 4 / 64];
                int acquisitionLength = 1000;
                RisingEdgeTrigger trigger = new(200, 190, 80000000);

                TriggeredCapture triggeredCapture = new() { Channels = Channels.Four, ChannelLength = acquisitionLength, ChannelData = new byte[acquisitionLength * 4] };
                bool tickTock = true;
                DateTimeOffset startTime = DateTimeOffset.UtcNow;

                int dequeueCounter = 0;
                uint triggerCount = 0;
                uint totalTriggerCount = 0;

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
                        // Horizontal Sum (if needed)
                        // Trigger
                        // Data segment on trigger (if needed)
                        case Channels.None:
                            break;
                        case Channels.One:
                            if (boxcarLength != BoxcarLength.None)
                                throw new NotImplementedException();
                            if (triggerChannel == TriggerChannel.One)
                                triggerCount = trigger.ProcessSimd(input: inputBuffer.Span, trigger: triggerResultBuffer_1);
                            //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}");

                            break;
                        case Channels.Two:
                            Shuffle.TwoChannels(input: inputBuffer.Span, output: shuffleBuffer);
                            if (boxcarLength != BoxcarLength.None)
                                throw new NotImplementedException();
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

                            //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}");

                            break;
                        case Channels.Four:
                            Shuffle.FourChannels(input: inputBuffer.Span, output: shuffleBuffer);
                            if (boxcarLength != BoxcarLength.None)
                                throw new NotImplementedException();
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
                                triggerCount = trigger.ProcessSimd(input: triggerChannelBuffer, trigger: triggerResultBuffer_4);
                                totalTriggerCount += triggerCount;
                                if (triggerCount > 0)
                                {
                                    var timeSpan = DateTimeOffset.UtcNow - startTime;
                                    Console.WriteLine($"{totalTriggerCount / timeSpan.TotalSeconds:F2} triggers/sec");

                                    // Scan through trigger buffer to find first trigger bit
                                    for (int i = 0; i < triggerResultBuffer_4.Length; i++)
                                    {
                                        // To do: need to store pre-trigger data so it can be prepended?
                                        if (triggerResultBuffer_4[i] > 0)
                                        {
                                            var index = (i * 64) + (64 - (int)Lzcnt.X64.LeadingZeroCount(triggerResultBuffer_4[i])) - 1;
                                            triggeredCapture.Semaphore.Wait(cancelToken);       // Wait until any downstream processing has released this memory
                                            postShuffleCh1_4.Slice(index, acquisitionLength).CopyTo(triggeredCapture.ChannelData.Span.Slice(0, acquisitionLength));
                                            postShuffleCh2_4.Slice(index, acquisitionLength).CopyTo(triggeredCapture.ChannelData.Span.Slice(acquisitionLength, acquisitionLength));
                                            postShuffleCh3_4.Slice(index, acquisitionLength).CopyTo(triggeredCapture.ChannelData.Span.Slice(acquisitionLength * 2, acquisitionLength));
                                            postShuffleCh4_4.Slice(index, acquisitionLength).CopyTo(triggeredCapture.ChannelData.Span.Slice(acquisitionLength * 3, acquisitionLength));
                                            triggeredCaptureWriter.TryWrite(triggeredCapture);
                                        }
                                    }
                                }
                            }
                            //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}, Ch3 triggers: {triggerCount3}, Ch4 triggers: {triggerCount4} ");
                            break;
                    }
                    tickTock = !tickTock;
                    //Thread.Sleep(3);      //Seems hardwareInput.Dequeue might be doing a spinwait?
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
