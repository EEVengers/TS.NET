using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace TS.NET.Engine
{
    // The job of this task is to read from the thunderscope as fast as possible with minimal jitter
    internal class InputTask
    {
        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public void Start(
            ILoggerFactory loggerFactory,
            ThunderscopeDevice thunderscopeDevice,
            BlockingChannelReader<ThunderscopeMemory> inputChannel,
            BlockingChannelWriter<InputDataDto> processingChannel,
            BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel)
        {
            var logger = loggerFactory.CreateLogger(nameof(InputTask));
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, thunderscopeDevice, inputChannel, processingChannel, hardwareRequestChannel, hardwareResponseChannel, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(
            ILogger logger,
            ThunderscopeDevice thunderscopeDevice,
            BlockingChannelReader<ThunderscopeMemory> inputChannel,
            BlockingChannelWriter<InputDataDto> processingChannel,
            BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel,
            CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = "TS.NET Input";
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            Thunderscope thunderscope = new();
            try
            {
                thunderscope.Open(thunderscopeDevice);
                ThunderscopeConfiguration configuration = DoInitialConfiguration(thunderscope);
                thunderscope.Start();
                logger.LogDebug("Started");

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
                                    ThunderscopeChannel channel = configuration.GetChannel(channelIndex);
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
                                    configuration.SetChannel(channelIndex, channel);
                                    ConfigureFromObject(thunderscope, configuration);
                                    thunderscope.EnableChannel(channelIndex);
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

                    var memory = inputChannel.Read(cancelToken);
                    while (true)
                    {
                        try
                        {
                            thunderscope.Read(memory);
                            if(enqueueCounter == 0)
                                logger.LogDebug("First block of data received");
                            break;
                        }
                        catch (ThunderscopeMemoryOutOfMemoryException)
                        {
                            logger.LogWarning("Scope ran out of memory - reset buffer pointers and continue");
                            thunderscope.ResetBuffer();
                            continue;
                        }
                        catch (ThunderscopeFIFOOverflowException)
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
                                logger.LogError(ex, $"{nameof(InputTask)} error");
                                continue;
                            }
                            throw;
                        }
                    }

                    periodicEnqueueCount++;
                    enqueueCounter++;

                    processingChannel.Write(new InputDataDto(configuration, memory), cancelToken);

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

        private static ThunderscopeConfiguration DoInitialConfiguration(Thunderscope thunderscope)
        {
            ThunderscopeConfiguration configuration = new()
            {
                AdcChannelMode = AdcChannelMode.Quad,
                Channel1 = ThunderscopeChannel.Default(),
                Channel2 = ThunderscopeChannel.Default(),
                Channel3 = ThunderscopeChannel.Default(),
                Channel4 = ThunderscopeChannel.Default(),
            };

            configuration.Channel2.VoltOffset = 0.5;

            ConfigureFromObject(thunderscope, configuration);

            thunderscope.EnableChannel(0);
            thunderscope.EnableChannel(1);
            thunderscope.EnableChannel(2);
            thunderscope.EnableChannel(3);

            return configuration;
        }

        private static void ConfigureFromObject(Thunderscope thunderscope, ThunderscopeConfiguration configuration)
        {
            for (int i = 0; i < 4; i++)
            {
                thunderscope.Channels[i] = configuration.GetChannel(i);
            }
        }
    }
}
