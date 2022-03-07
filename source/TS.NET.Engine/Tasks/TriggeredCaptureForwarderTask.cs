using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cloudtoid.Interprocess;

namespace TS.NET.Engine
{
    public class TriggeredCaptureForwarderTask
    {
        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoopInput;
        private Task? taskLoopOutput;

        //, Action<Memory<double>> action
        public void Start(ILoggerFactory loggerFactory, BlockingChannelReader<TriggeredCapture> triggeredCaptureReader)
        {
            var logger = loggerFactory.CreateLogger(nameof(TriggeredCaptureForwarderTask));
            cancelTokenSource = new CancellationTokenSource();
            taskLoopInput = Task.Factory.StartNew(() => LoopInput(logger, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
            taskLoopOutput = Task.Factory.StartNew(() => LoopOutput(logger, loggerFactory, 8000000, triggeredCaptureReader, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoopOutput?.Wait();
        }

        // The job of this task - pull data from triggered captures and send over inter-process to UI
        private static void LoopOutput(ILogger logger, ILoggerFactory loggerFactory, int inputLength, BlockingChannelReader<TriggeredCapture> triggeredCaptureReader, CancellationToken cancelToken)
        {
            
            Span<byte> buffer = new byte[10 * 1024 * 1024];
            try
            {
                // This is an inter-process high-performance queue using memory-mapped file & semaphore. Used here to send data to UI.
                var queueFactory = new QueueFactory();
                var queueOptions = new QueueOptions(queueName: "ThunderScopeTriggeredCaptureForwarderOutput", bytesCapacity: 2 * 8000000);
                using var forwarder = queueFactory.CreatePublisher(queueOptions);

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (triggeredCaptureReader.TryRead(out var item, cancelToken))
                    {
                            forwarder.TryEnqueue(new TriggeredCaptureDto() { Channels = item.Channels, ChannelLength = item.ChannelLength, ChannelData = item.ChannelData }, buffer);
                        item.Semaphore.Release();       // End of the pipeline; release data for reuse
                    }
                    
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(TriggeredCaptureForwarderTask)} stopping");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(TriggeredCaptureForwarderTask)} error");
                throw;
            }
            finally
            {
                logger.LogDebug($"{nameof(TriggeredCaptureForwarderTask)} stopped");
            }
        }

        private static void LoopInput(ILogger logger, CancellationToken cancelToken)
        {
            Memory<byte> buffer = new byte[10 * 1024 * 1024];
            try
            {
                // This is an inter-process high-performance queue using memory-mapped file & semaphore. Used here to send data to UI.
                var queueFactory = new QueueFactory();
                var queueOptions = new QueueOptions(queueName: "ThunderScopeTriggeredCaptureForwarderInput", bytesCapacity: 2 * 8000000);
                using var forwarder = queueFactory.CreateSubscriber(queueOptions);

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    var dtoBytes = forwarder.Dequeue(buffer, cancelToken, out string dtoName);
                    switch (dtoName)
                    {
                        case nameof(DisplayedDto):
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(TriggeredCaptureForwarderTask)} stopping");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(TriggeredCaptureForwarderTask)} error");
                throw;
            }
            finally
            {
                logger.LogDebug($"{nameof(TriggeredCaptureForwarderTask)} stopped");
            }
        }
    }
}
