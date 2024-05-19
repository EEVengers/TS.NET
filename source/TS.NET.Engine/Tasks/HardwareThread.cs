using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TS.NET.Engine
{
    // The job of this task is to read from the thunderscope as fast as possible with minimal jitter
    internal class HardwareThread
    {
        private readonly ILogger logger;
        private readonly IThunderscope thunderscope;
        private readonly ThunderscopeSettings settings;
        private readonly BlockingChannelReader<ThunderscopeMemory> inputChannel;
        private readonly BlockingChannelWriter<InputDataDto> processingChannel;
        private readonly BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public HardwareThread(ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            IThunderscope thunderscope,
            BlockingChannelReader<ThunderscopeMemory> inputChannel,
            BlockingChannelWriter<InputDataDto> processingChannel,
            BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel)
        {
            logger = loggerFactory.CreateLogger(nameof(HardwareThread));
            this.settings = settings;
            this.thunderscope = thunderscope;
            this.inputChannel = inputChannel;
            this.processingChannel = processingChannel;
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.hardwareResponseChannel = hardwareResponseChannel;
        }

        public void Start()
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, thunderscope, settings, inputChannel, processingChannel, hardwareRequestChannel, hardwareResponseChannel, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(
            ILogger logger,
            IThunderscope thunderscope,
            ThunderscopeSettings settings,
            BlockingChannelReader<ThunderscopeMemory> inputChannel,
            BlockingChannelWriter<InputDataDto> processingChannel,
            BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel,
            CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = nameof(HardwareThread);
            //Thread.CurrentThread.Priority = ThreadPriority.Highest;
            if (settings.HardwareThreadProcessorAffinity > -1 && OperatingSystem.IsWindows())
            {
                Thread.BeginThreadAffinity();
                Interop.CurrentThread.ProcessorAffinity = new IntPtr(1 << settings.HardwareThreadProcessorAffinity);
                logger.LogDebug($"{nameof(HardwareThread)} thread processor affinity set to {settings.HardwareThreadProcessorAffinity}");
            }
            
            try
            {               
                thunderscope.Start();
                logger.LogInformation("Started");

                logger.LogDebug("Waiting for first block of data...");

                Stopwatch periodicUpdateTimer = Stopwatch.StartNew();
                uint periodicEnqueueCount = 0;
                uint enqueueCounter = 0;

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    // Check for configuration requests
                    if (hardwareRequestChannel.PeekAvailable() != 0)
                    {
                        logger.LogDebug("Stop acquisition and process commands...");
                        thunderscope.Stop();

                        while (hardwareRequestChannel.TryRead(out var request))
                        {
                            // Do configuration update, pausing acquisition if necessary
                            switch (request)
                            {
                                case HardwareStartRequest hardwareStartRequest:
                                    logger.LogDebug("Start request (ignore)");
                                    break;
                                case HardwareStopRequest hardwareStopRequest:
                                    logger.LogDebug("Stop request (ignore)");
                                    break;
                                case HardwareConfigureChannelDto hardwareConfigureChannelDto:
                                    var channelIndex = ((HardwareConfigureChannelDto)request).Channel;
                                    ThunderscopeChannel channel = thunderscope.GetChannel(channelIndex);
                                    switch (request)
                                    {
                                        case HardwareSetVoltOffsetRequest hardwareSetOffsetRequest:
                                            logger.LogDebug($"Set offset request: channel {channelIndex} volt offset {hardwareSetOffsetRequest.VoltOffset}");
                                            channel.VoltOffset = hardwareSetOffsetRequest.VoltOffset;
                                            break;
                                        case HardwareSetVoltFullScaleRequest hardwareSetVdivRequest:
                                            logger.LogDebug($"Set vdiv request: channel {channelIndex} volt full scale {hardwareSetVdivRequest.VoltFullScale}");
                                            channel.VoltFullScale = hardwareSetVdivRequest.VoltFullScale;
                                            break;
                                        case HardwareSetBandwidthRequest hardwareSetBandwidthRequest:
                                            logger.LogDebug($"Set bw request: channel {channelIndex} bandwidth {hardwareSetBandwidthRequest.Bandwidth}");
                                            channel.Bandwidth = hardwareSetBandwidthRequest.Bandwidth;
                                            break;
                                        case HardwareSetCouplingRequest hardwareSetCouplingRequest:
                                            logger.LogDebug($"Set coup request: channel {channelIndex} coupling {hardwareSetCouplingRequest.Coupling}");
                                            channel.Coupling = hardwareSetCouplingRequest.Coupling;
                                            break;
                                        case HardwareSetEnabledRequest hardwareSetEnabledRequest:
                                            logger.LogDebug($"Set enabled request: channel {channelIndex} enabled {hardwareSetEnabledRequest.Enabled}");
                                            channel.Enabled = hardwareSetEnabledRequest.Enabled;
                                            break;
                                        default:
                                            logger.LogWarning($"Unknown HardwareConfigureChannelDto: {request}");
                                            break;
                                    }
                                    thunderscope.SetChannel(channel, channelIndex);
                                    break;
                                default:
                                    logger.LogWarning($"Unknown HardwareRequestDto: {request}");
                                    break;
                            }
                            // Signal back to the sender that config update happened.
                            // hardwareResponseChannel.TryWrite(new HardwareResponseDto(request));

                            if (hardwareRequestChannel.PeekAvailable() == 0)
                                Thread.Sleep(150);
                        }

                        logger.LogDebug("Start again");
                        thunderscope.Start();
                    }

                    //logger.LogDebug($"Requesting memory block {enqueueCounter}");
                    var memory = inputChannel.Read(cancelToken);
                    //logger.LogDebug($"Memory block {enqueueCounter}");
                    while (true)
                    {
                        try
                        {
                            thunderscope.Read(memory, cancelToken);
                            if (enqueueCounter == 0)
                                logger.LogDebug("First block of data received");
                            //logger.LogDebug($"Acquisition block {enqueueCounter}");
                            break;
                        }
                        catch (ThunderscopeMemoryOutOfMemoryException)
                        {
                            logger.LogWarning("Scope ran out of memory - reset buffer pointers and continue");
                            ((Driver.XMDA.Thunderscope)thunderscope).ResetBuffer();
                            continue;
                        }
                        catch (ThunderscopeFifoOverflowException)
                        {
                            logger.LogWarning("Scope had FIFO overflow - ignore and continue");
                            continue;
                        }
                        catch (ThunderscopeNotRunningException)
                        {
                            // logger.LogWarning("Tried to read from stopped scope");
                            continue;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message == "ReadFile - failed (1359)")
                            {
                                logger.LogError(ex, $"{nameof(HardwareThread)} error");
                                continue;
                            }
                            throw;
                        }
                    }

                    periodicEnqueueCount++;
                    enqueueCounter++;

                    processingChannel.Write(new InputDataDto(thunderscope.GetConfiguration(), memory), cancelToken);

                    if (periodicUpdateTimer.ElapsedMilliseconds >= 10000)
                    {
                        var oneSecondEnqueueCount = periodicEnqueueCount / periodicUpdateTimer.Elapsed.TotalSeconds;
                        logger.LogDebug($"Enqueues/sec: {oneSecondEnqueueCount:F2}, MB/sec: {(oneSecondEnqueueCount * ThunderscopeMemory.Length / 1000 / 1000):F3}, MiB/sec: {(oneSecondEnqueueCount * ThunderscopeMemory.Length / 1024 / 1024):F3}, enqueue count: {enqueueCounter}");
                        periodicUpdateTimer.Restart();
                        periodicEnqueueCount = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Stopping...");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error");
                throw;
            }
            finally
            {
                thunderscope.Stop();
                logger.LogDebug("Stopped");
            }
        }
    }
}
