﻿using Microsoft.Extensions.Logging;
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
            var logger = loggerFactory.CreateLogger("InputTask");
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
                logger.LogInformation("Starting...");
                thunderscope.Open(thunderscopeDevice);
                ThunderscopeConfiguration configuration = DoInitialConfiguration(thunderscope);
                thunderscope.Start();
                logger.LogInformation("Started");

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
                                    var chNum = ((HardwareConfigureChannelDto)request).Channel;
                                    ThunderscopeChannel ch = configuration.GetChannel(chNum);
                                    switch (request)
                                    {
                                        case HardwareSetOffsetRequest hardwareSetOffsetRequest:
                                            var voltage = hardwareSetOffsetRequest.Offset;
                                            logger.LogDebug($"Set offset request: ch {chNum} voltage {voltage}");
                                            ch.VoltsOffset = voltage;
                                            break;
                                        case HardwareSetVdivRequest hardwareSetVdivRequest:
                                            var vdiv = hardwareSetVdivRequest.VoltsDiv;
                                            logger.LogDebug($"Set vdiv request: ch {chNum} div {vdiv}");
                                            ch.VoltsDiv = vdiv;
                                            break;
                                        case HardwareSetBandwidthRequest hardwareSetBandwidthRequest:
                                            var bw = hardwareSetBandwidthRequest.Bandwidth;
                                            logger.LogDebug($"Set bw request: ch {chNum} bw {bw}");
                                            ch.Bandwidth = bw;
                                            break;
                                        case HardwareSetCouplingRequest hardwareSetCouplingRequest:
                                            var coup = hardwareSetCouplingRequest.Coupling;
                                            logger.LogDebug($"Set coup request: ch {chNum} coup {coup}");
                                            ch.Coupling = coup;
                                            break;
                                        case HardwareSetEnabledRequest hardwareSetEnabledRequest:
                                            var enabled = ((HardwareSetEnabledRequest)request).Enabled;
                                            logger.LogDebug($"Set enabled request: ch {chNum} enabled {enabled}");
                                            ch.Enabled = enabled;
                                            break;
                                        default:
                                            logger.LogWarning($"Unknown HardwareConfigureChannelDto: {request}");
                                            break;
                                    }
                                    configuration.SetChannel(chNum, ch);
                                    ConfigureFromObject(thunderscope, configuration);
                                    thunderscope.EnableChannel(chNum);
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
                            break;
                        }
                        catch (ThunderscopeMemoryOutOfMemoryException ex)
                        {
                            logger.LogWarning("Scope ran out of memory - reset buffer pointers and continue");
                            thunderscope.ResetBuffer();
                            continue;
                        }
                        catch (ThunderscopeFIFOOverflowException ex)
                        {
                            logger.LogWarning("Scope had FIFO overflow - ignore and continue");
                            continue;
                        }
                        catch (ThunderscopeNotRunningException ex)
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
                AdcChannels = AdcChannels.Four,
                Channel0 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 5000,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC,
                    Term50Z = false
                },
                Channel1 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 5000,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC,
                    Term50Z = false
                },
                Channel2 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 5000,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC,
                    Term50Z = false
                },
                Channel3 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 5000,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC,
                    Term50Z = false
                },
            };

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
