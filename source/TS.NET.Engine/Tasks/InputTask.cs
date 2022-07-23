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
                thunderscope.Open(thunderscopeDevice);
                ThunderscopeConfiguration configuration = DoInitialConfiguration(thunderscope);
                thunderscope.Start();

                Stopwatch oneSecond = Stopwatch.StartNew();
                uint oneSecondEnqueueCount = 0;
                uint enqueueCounter = 0;

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    // Check for configuration requests
                    if (hardwareRequestChannel.TryRead(out var request))
                    {
                        // Do configuration update, pausing acquisition if necessary (TBD)

                        // Signal back to the sender that config update happened.
                        hardwareResponseChannel.TryWrite(new HardwareResponseDto(request.Command));
                    }

                    var memory = inputChannel.Read();

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

                    oneSecondEnqueueCount++;
                    enqueueCounter++;

                    processingChannel.Write(new InputDataDto(configuration, memory), cancelToken);

                    if (oneSecond.ElapsedMilliseconds >= 1000)
                    {
                        logger.LogDebug($"Enqueues/sec: {oneSecondEnqueueCount / (oneSecond.ElapsedMilliseconds * 0.001):F2}, enqueue count: {enqueueCounter}");
                        oneSecond.Restart();
                        oneSecondEnqueueCount = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug($"{nameof(InputTask)} stopping");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, $"{nameof(InputTask)} error");
                throw;
            }
            finally
            {
                thunderscope.Stop();
                logger.LogDebug($"{nameof(InputTask)} stopped");
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
                    VoltsDiv = 100,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC
                },
                Channel1 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 100,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC
                },
                Channel2 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 100,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC
                },
                Channel3 = new ThunderscopeChannel()
                {
                    Enabled = true,
                    VoltsOffset = 0,
                    VoltsDiv = 100,
                    Bandwidth = 350,
                    Coupling = ThunderscopeCoupling.DC
                },
            };

            thunderscope.EnableChannel(0);
            thunderscope.EnableChannel(1);
            thunderscope.EnableChannel(2);
            thunderscope.EnableChannel(3);

            return configuration;
        }
    }
}
