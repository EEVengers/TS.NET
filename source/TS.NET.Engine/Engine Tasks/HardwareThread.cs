using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TS.NET.Engine
{
    // The job of this task is to read from the thunderscope as fast as possible with minimal jitter
    internal class HardwareThread : IEngineTask
    {
        private readonly ILogger logger;
        private readonly IThunderscope thunderscope;
        private readonly ThunderscopeSettings settings;
        private readonly BlockingChannelReader<ThunderscopeMemory> inputChannel;
        private readonly BlockingChannelWriter<InputDataDto> processChannel;
        private readonly BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public HardwareThread(ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            IThunderscope thunderscope,
            BlockingChannelReader<ThunderscopeMemory> inputChannel,
            BlockingChannelWriter<InputDataDto> processChannel,
            BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel)
        {
            logger = loggerFactory.CreateLogger(nameof(HardwareThread));
            this.settings = settings;
            this.thunderscope = thunderscope;
            this.inputChannel = inputChannel;
            this.processChannel = processChannel;
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.hardwareResponseChannel = hardwareResponseChannel;
        }

        public void Start(SemaphoreSlim startSemaphore)
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, thunderscope, settings, inputChannel, processChannel, hardwareRequestChannel, hardwareResponseChannel, startSemaphore, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
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
            BlockingChannelWriter<InputDataDto> processChannel,
            BlockingChannelReader<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<HardwareResponseDto> hardwareResponseChannel,
            SemaphoreSlim startSemaphore,
            CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = "Hardware";
            //Thread.CurrentThread.Priority = ThreadPriority.Highest;
            if (settings.HardwareThreadProcessorAffinity > -1 && OperatingSystem.IsWindows())
            {
                Thread.BeginThreadAffinity();
                OsThread.SetThreadAffinity(settings.HardwareThreadProcessorAffinity);
                logger.LogDebug($"{nameof(HardwareThread)} thread processor affinity set to {settings.HardwareThreadProcessorAffinity}");
            }

            try
            {
                thunderscope.Start();
                logger.LogInformation("Started");
                startSemaphore.Release();

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
                                case HardwareSetRateRequest hardwareSetRateRequest:
                                    {
                                        thunderscope.Stop();    // To do: determine if this is needed
                                        thunderscope.SetRate(hardwareSetRateRequest.rate);
                                        thunderscope.Start();
                                        logger.LogDebug($"{nameof(hardwareSetRateRequest)} (rate: {hardwareSetRateRequest.rate})");
                                        break;
                                    }
                                case HardwareGetRateRequest hardwareGetRateRequest:
                                    {
                                        logger.LogDebug($"{nameof(HardwareGetRateRequest)}");
                                        var config = thunderscope.GetConfiguration();
                                        hardwareResponseChannel.Write(new HardwareGetRateResponse(config.SampleRateHz));
                                        logger.LogDebug($"{nameof(HardwareGetRateResponse)}");
                                        break;
                                    }
                                case HardwareGetRatesRequest hardwareGetRatesRequest:
                                    {
                                        logger.LogDebug($"{nameof(HardwareGetRatesRequest)}");
                                        var config = thunderscope.GetConfiguration();
                                        // Create driver & hardware configuration specific response
                                        List<ulong> rates = [];
                                        switch (thunderscope)
                                        {
                                            case Driver.XMDA.Thunderscope xdmaThunderscope:
                                                {
                                                    switch (config.AdcChannelMode)
                                                    {
                                                        case AdcChannelMode.Single:
                                                            rates.Add(1000000000);
                                                            break;
                                                        case AdcChannelMode.Dual:
                                                            rates.Add(500000000);
                                                            break;
                                                        case AdcChannelMode.Quad:
                                                            rates.Add(250000000);
                                                            break;
                                                    }
                                                    break;
                                                }
                                            case Driver.Libtslitex.Thunderscope liteXThunderscope:
                                                {
                                                    switch (config.AdcChannelMode)
                                                    {
                                                        case AdcChannelMode.Single:
                                                            rates.Add(1000000000);
                                                            rates.Add(500000000);
                                                            rates.Add(250000000);
                                                            rates.Add(100000000);
                                                            rates.Add(50000000);
                                                            rates.Add(25000000);
                                                            break;
                                                        case AdcChannelMode.Dual:
                                                            rates.Add(500000000);
                                                            rates.Add(250000000);
                                                            rates.Add(100000000);
                                                            rates.Add(50000000);
                                                            rates.Add(25000000);
                                                            break;
                                                        case AdcChannelMode.Quad:
                                                            rates.Add(250000000);
                                                            rates.Add(100000000);
                                                            rates.Add(50000000);
                                                            rates.Add(25000000);
                                                            break;
                                                    }
                                                    break;
                                                }
                                        }
                                        hardwareResponseChannel.Write(new HardwareGetRatesResponse(rates.ToArray()));
                                        logger.LogDebug($"{nameof(HardwareGetRatesResponse)}");
                                        break;
                                    }
                                case HardwareSetChannelFrontendRequest hardwareConfigureChannelFrontendDto:
                                    {
                                        var channelIndex = ((HardwareSetChannelFrontendRequest)request).ChannelIndex;
                                        var channelFrontend = thunderscope.GetChannelFrontend(channelIndex);
                                        channelFrontend.PgaConfigWordOverride = false;
                                        switch (request)
                                        {
                                            case HardwareSetVoltOffsetRequest hardwareSetOffsetRequest:
                                                logger.LogDebug($"{nameof(HardwareSetVoltOffsetRequest)} (channel: {channelIndex}, offset: {hardwareSetOffsetRequest.VoltOffset})");
                                                channelFrontend.VoltOffset = hardwareSetOffsetRequest.VoltOffset;
                                                break;
                                            case HardwareSetVoltFullScaleRequest hardwareSetVdivRequest:
                                                logger.LogDebug($"{nameof(HardwareSetVoltFullScaleRequest)} (channel: {channelIndex}, scale: {hardwareSetVdivRequest.VoltFullScale})");
                                                channelFrontend.VoltFullScale = hardwareSetVdivRequest.VoltFullScale;
                                                break;
                                            case HardwareSetBandwidthRequest hardwareSetBandwidthRequest:
                                                logger.LogDebug($"{nameof(HardwareSetBandwidthRequest)} (channel: {channelIndex}, bandwidth: {hardwareSetBandwidthRequest.Bandwidth})");
                                                channelFrontend.Bandwidth = hardwareSetBandwidthRequest.Bandwidth;
                                                break;
                                            case HardwareSetCouplingRequest hardwareSetCouplingRequest:
                                                logger.LogDebug($"{nameof(HardwareSetCouplingRequest)} (channel: {channelIndex}, coupling: {hardwareSetCouplingRequest.Coupling})");
                                                channelFrontend.Coupling = hardwareSetCouplingRequest.Coupling;
                                                break;
                                            case HardwareSetEnabledRequest hardwareSetEnabledRequest:
                                                logger.LogDebug($"{nameof(HardwareSetEnabledRequest)} (channel: {channelIndex}, enabled: {hardwareSetEnabledRequest.Enabled})");
                                                thunderscope.Stop();    // To do: determine if this is needed
                                                thunderscope.SetChannelEnable(channelIndex, hardwareSetEnabledRequest.Enabled);
                                                thunderscope.Start();
                                                break;
                                            case HardwareSetTerminationRequest hardwareSetTerminationRequest:
                                                logger.LogDebug($"{nameof(HardwareSetTerminationRequest)} (channel: {channelIndex}, termination: {hardwareSetTerminationRequest.Termination})");
                                                channelFrontend.Termination = hardwareSetTerminationRequest.Termination;
                                                break;
                                            default:
                                                logger.LogWarning($"Unknown {nameof(HardwareSetChannelFrontendRequest)}: {request}");
                                                break;
                                        }
                                        thunderscope.SetChannelFrontend(channelIndex, channelFrontend);
                                        break;
                                    }
                                case HardwareSetChannelCalibrationRequest hardwareSetChannelCalibrationDto:
                                    {
                                        var channelIndex = ((HardwareSetChannelCalibrationRequest)request).ChannelIndex;
                                        var channelCalibration = thunderscope.GetChannelCalibration(channelIndex);
                                        switch (request)
                                        {
                                            case HardwareSetOffsetVoltageLowGainRequest hardwareSetOffsetVoltageLowGainRequest:
                                                logger.LogDebug($"{nameof(HardwareSetOffsetVoltageLowGainRequest)} (channel: {channelIndex})");
                                                channelCalibration.HardwareOffsetVoltageLowGain = hardwareSetOffsetVoltageLowGainRequest.OffsetVoltage;     // XDMA, example value: 2.501
                                                channelCalibration.PgaLowOffsetVoltage = hardwareSetOffsetVoltageLowGainRequest.OffsetVoltage;              // LiteX
                                                break;
                                            case HardwareSetOffsetVoltageHighGainRequest hardwareSetOffsetVoltageHighGainRequest:
                                                logger.LogDebug($"{nameof(HardwareSetOffsetVoltageHighGainRequest)} (channel: {channelIndex})");
                                                channelCalibration.HardwareOffsetVoltageHighGain = hardwareSetOffsetVoltageHighGainRequest.OffsetVoltage;   // XMDA, example value: 2.501
                                                channelCalibration.PgaHighOffsetVoltage = hardwareSetOffsetVoltageHighGainRequest.OffsetVoltage;            // LiteX
                                                break;
                                        }
                                        thunderscope.SetChannelCalibration(channelIndex, channelCalibration);
                                        break;
                                    }
                                case HardwareSetChannelManualControlRequest hardwareSetChannelManualControlRequest:
                                    {
                                        var channelIndex = hardwareSetChannelManualControlRequest.ChannelIndex;
                                        var channel = hardwareSetChannelManualControlRequest.Channel;

                                        ((Driver.Libtslitex.Thunderscope)thunderscope).SetChannelManualControl(channelIndex, channel);
                                        break;
                                    }
                                case HardwareSetAdcCalibrationRequest hardwareSetAdcCalibrationRequest:
                                    {
                                        ((Driver.Libtslitex.Thunderscope)thunderscope).SetAdcCalibration(hardwareSetAdcCalibrationRequest.AdcCalibration);
                                        break;
                                    }
                                default:
                                    logger.LogWarning($"Unknown {nameof(HardwareRequestDto)}: {request}");
                                    break;
                            }
                            if (hardwareRequestChannel.PeekAvailable() == 0)
                                Thread.Sleep(150);
                        }
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

                    processChannel.Write(new InputDataDto(thunderscope.GetConfiguration(), memory), cancelToken);

                    if (periodicUpdateTimer.ElapsedMilliseconds >= 10000)
                    {
                        var oneSecondEnqueueCount = periodicEnqueueCount / periodicUpdateTimer.Elapsed.TotalSeconds;
                        logger.LogDebug($"[Stream] MB/sec: {(oneSecondEnqueueCount * ThunderscopeMemory.Length / 1000 / 1000):F3}, MiB/sec: {(oneSecondEnqueueCount * ThunderscopeMemory.Length / 1024 / 1024):F3}");

                        if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                        {
                            var status = liteXThunderscope.GetStatus();
                            logger.LogDebug($"[LiteX] lost buffers: {status.AdcSamplesLost}, temp: {status.FpgaTemp:F2}, VCC int: {status.VccInt:F3}, VCC aux: {status.VccAux:F3}, VCC BRAM: {status.VccBram:F3}");
                        }

                        periodicUpdateTimer.Restart();
                        periodicEnqueueCount = 0;
                    }

                    // Ideally sleep would only occur after driver reports that it doesn't have enough bytes available to read in a read-while-loop.
                    // Something like "while(thunderscope.BytesAvailable() > ThunderscopeMemory.Length) { thunderscope.Read(...); }"
                    // In this hypothetical case, the sleep should be longer (something like 20ms; longer than 8.39ms to ensure next thread iteration has data).
                    // For now, do 4ms to reduce the reported CPU usage a bit, without falling behind (hopefully, not guaranteed).
                    //Thread.Sleep(4);    // At 1GSPS, the Read will return every 8.39ms. (1000/(1000000000/8388608))
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
