using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TS.NET.Engine;

// The job of this task is to read from the thunderscope as fast as possible with minimal jitter
internal class HardwareThread : IThread
{
    private readonly ILogger logger;
    private readonly IThunderscope thunderscope;
    private readonly ThunderscopeSettings settings;
    private readonly BlockingPool<DataDto> hardwarePool;
    private readonly BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskLoop;

    public HardwareThread(ILogger logger,
        ThunderscopeSettings settings,
        IThunderscope thunderscope,
        BlockingPool<DataDto> hardwarePool,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl)
    {
        this.logger = logger;
        this.settings = settings;
        this.thunderscope = thunderscope;
        this.hardwarePool = hardwarePool;
        this.hardwareControl = hardwareControl;
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        cancelTokenSource = new CancellationTokenSource();
        taskLoop = Task.Factory.StartNew(() => Loop(
            logger: logger,
            thunderscope: thunderscope,
            settings: settings,
            hardwarePool: hardwarePool,
            hardwareControl: hardwareControl,
            startSemaphore,
            cancelTokenSource.Token), TaskCreationOptions.LongRunning);
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
        BlockingPool<DataDto> hardwarePool,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
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
            logger.LogInformation("Started");
            startSemaphore.Release();

            logger.LogDebug("Waiting for first block of data...");

            Stopwatch periodicUpdateTimer = Stopwatch.StartNew();
            uint periodicEnqueueCount = 0;
            uint enqueueCounter = 0;
            var dataDto = hardwarePool.Return.Reader.Read(cancelToken);
            dataDto.Memory.Reset();
            bool validDataDto = true;
            var resolution = AdcResolution.EightBit;
            var dataType = ThunderscopeDataType.I8;
#if DEBUG
            thunderscope.Start();
#endif

            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                if (hardwareControl.Request.Reader.PeekAvailable() > 0)
                {
                    while (hardwareControl.Request.Reader.TryRead(out var request))
                    {
                        // Do configuration update, pausing acquisition if necessary
                        switch (request)
                        {
                            case HardwareStartRequest hardwareStartRequest:
                                thunderscope.Start();
                                logger.LogDebug($"{nameof(HardwareStartRequest)}");
                                break;
                            case HardwareStopRequest hardwareStopRequest:
                                thunderscope.Stop();
                                hardwareControl.Response.Writer.Write(new HardwareStopResponse());
                                logger.LogDebug($"{nameof(HardwareStopRequest)}");
                                break;
                            case HardwareSetRateRequest hardwareSetRateRequest:
                                {
                                    thunderscope.SetRate(hardwareSetRateRequest.Rate);
                                    logger.LogDebug($"{nameof(HardwareSetRateRequest)} (rate: {hardwareSetRateRequest.Rate})");
                                    break;
                                }
                            case HardwareSetResolutionRequest hardwareSetResolutionRequest:
                                {
                                    resolution = hardwareSetResolutionRequest.Resolution;
                                    dataType = resolution switch
                                    {
                                        AdcResolution.EightBit => ThunderscopeDataType.I8,
                                        AdcResolution.TwelveBit => ThunderscopeDataType.I16,
                                        _ => throw new NotImplementedException()
                                    };
                                    thunderscope.SetResolution(hardwareSetResolutionRequest.Resolution);
                                    logger.LogDebug($"{nameof(HardwareSetResolutionRequest)} (resolution: {hardwareSetResolutionRequest.Resolution})");
                                    break;
                                }
                            case HardwareGetRateRequest hardwareGetRateRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetRateRequest)}");
                                    var config = thunderscope.GetConfiguration();
                                    hardwareControl.Response.Writer.Write(new HardwareGetRateResponse(config.SampleRateHz));
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
                                        case Driver.Libtslitex.Thunderscope liteXThunderscope:
                                            {
                                                switch (config.AdcChannelMode)
                                                {
                                                    case AdcChannelMode.Single:
                                                        rates.Add(1_000_000_000);
                                                        rates.Add(660_000_000);
                                                        rates.Add(500_000_000);
                                                        rates.Add(330_000_000);
                                                        rates.Add(250_000_000);
                                                        rates.Add(165_000_000);
                                                        rates.Add(100_000_000);
                                                        break;
                                                    case AdcChannelMode.Dual:
                                                        rates.Add(500_000_000);
                                                        rates.Add(330_000_000);
                                                        rates.Add(250_000_000);
                                                        rates.Add(165_000_000);
                                                        rates.Add(100_000_000);
                                                        break;
                                                    case AdcChannelMode.Quad:
                                                        rates.Add(250_000_000);
                                                        rates.Add(165_000_000);
                                                        rates.Add(100_000_000);
                                                        break;
                                                }
                                                break;
                                            }
                                        case Driver.Simulation.Thunderscope simulationThunderscope:
                                            {
                                                rates.Add(1000000000);
                                                break;
                                            }
                                    }
                                    hardwareControl.Response.Writer.Write(new HardwareGetRatesResponse(rates.ToArray()));
                                    logger.LogDebug($"{nameof(HardwareGetRatesResponse)}");
                                    break;
                                }
                            case HardwareGetResolutionRequest hardwareGetResolutionRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetResolutionRequest)}");
                                    var config = thunderscope.GetConfiguration();
                                    hardwareControl.Response.Writer.Write(new HardwareGetResolutionResponse(config.Resolution));
                                    logger.LogDebug($"{nameof(HardwareGetResolutionResponse)}");
                                    break;
                                }
                            case HardwareGetEnabledRequest hardwareGetEnabledRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetEnabledRequest)}");
                                    var config = thunderscope.GetConfiguration();
                                    var enabled = ((config.EnabledChannels >> hardwareGetEnabledRequest.ChannelIndex) & 0x01) > 0;
                                    hardwareControl.Response.Writer.Write(new HardwareGetEnabledResponse(enabled));
                                    logger.LogDebug($"{nameof(HardwareGetEnabledResponse)}");
                                    break;
                                }
                            case HardwareGetVoltOffsetRequest hardwareGetVoltOffsetRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetVoltOffsetRequest)}");
                                    var frontend = thunderscope.GetChannelFrontend(hardwareGetVoltOffsetRequest.ChannelIndex);
                                    hardwareControl.Response.Writer.Write(new HardwareGetVoltOffsetResponse(frontend.RequestedVoltOffset, frontend.ActualVoltOffset));
                                    logger.LogDebug($"{nameof(HardwareGetVoltOffsetRequest)}");
                                    break;
                                }
                            case HardwareGetVoltFullScaleRequest hardwareGetVoltFullScaleRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetVoltFullScaleRequest)}");
                                    var frontend = thunderscope.GetChannelFrontend(hardwareGetVoltFullScaleRequest.ChannelIndex);
                                    hardwareControl.Response.Writer.Write(new HardwareGetVoltFullScaleResponse(frontend.RequestedVoltFullScale, frontend.ActualVoltFullScale));
                                    logger.LogDebug($"{nameof(HardwareGetVoltFullScaleRequest)}");
                                    break;
                                }
                            case HardwareGetBandwidthRequest hardwareGetBandwidthRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetBandwidthRequest)}");
                                    var frontend = thunderscope.GetChannelFrontend(hardwareGetBandwidthRequest.ChannelIndex);
                                    hardwareControl.Response.Writer.Write(new HardwareGetBandwidthResponse(frontend.Bandwidth));
                                    logger.LogDebug($"{nameof(HardwareGetBandwidthRequest)}");
                                    break;
                                }
                            case HardwareGetCouplingRequest hardwareGetCouplingRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetCouplingRequest)}");
                                    var frontend = thunderscope.GetChannelFrontend(hardwareGetCouplingRequest.ChannelIndex);
                                    hardwareControl.Response.Writer.Write(new HardwareGetCouplingResponse(frontend.Coupling));
                                    logger.LogDebug($"{nameof(HardwareGetCouplingRequest)}");
                                    break;
                                }
                            case HardwareGetTerminationRequest hardwareGetTerminationRequest:
                                {
                                    logger.LogDebug($"{nameof(HardwareGetTerminationRequest)}");
                                    var frontend = thunderscope.GetChannelFrontend(hardwareGetTerminationRequest.ChannelIndex);
                                    hardwareControl.Response.Writer.Write(new HardwareGetTerminationResponse(frontend.Termination));
                                    logger.LogDebug($"{nameof(HardwareGetTerminationRequest)}");
                                    break;
                                }
                            case HardwareSetEnabledRequest hardwareSetEnabledRequest:
                                {
                                    var channelIndex = ((HardwareSetEnabledRequest)request).ChannelIndex;
                                    logger.LogDebug($"{nameof(HardwareSetEnabledRequest)} (channel: {channelIndex}, enabled: {hardwareSetEnabledRequest.Enabled})");
                                    thunderscope.SetChannelEnable(channelIndex, hardwareSetEnabledRequest.Enabled);
                                    break;
                                }
                            case HardwareSetChannelFrontendRequest hardwareConfigureChannelFrontendDto:
                                {
                                    var channelIndex = ((HardwareSetChannelFrontendRequest)request).ChannelIndex;
                                    var channelFrontend = thunderscope.GetChannelFrontend(channelIndex);
                                    switch (request)
                                    {
                                        case HardwareSetVoltOffsetRequest hardwareSetOffsetRequest:
                                            logger.LogDebug($"{nameof(HardwareSetVoltOffsetRequest)} (channel: {channelIndex}, offset: {hardwareSetOffsetRequest.VoltOffset})");
                                            channelFrontend.RequestedVoltOffset = hardwareSetOffsetRequest.VoltOffset;
                                            break;
                                        case HardwareSetVoltFullScaleRequest hardwareSetVdivRequest:
                                            logger.LogDebug($"{nameof(HardwareSetVoltFullScaleRequest)} (channel: {channelIndex}, scale: {hardwareSetVdivRequest.VoltFullScale})");
                                            channelFrontend.RequestedVoltFullScale = hardwareSetVdivRequest.VoltFullScale;
                                            break;
                                        case HardwareSetBandwidthRequest hardwareSetBandwidthRequest:
                                            logger.LogDebug($"{nameof(HardwareSetBandwidthRequest)} (channel: {channelIndex}, bandwidth: {hardwareSetBandwidthRequest.Bandwidth})");
                                            channelFrontend.Bandwidth = hardwareSetBandwidthRequest.Bandwidth;
                                            break;
                                        case HardwareSetCouplingRequest hardwareSetCouplingRequest:
                                            logger.LogDebug($"{nameof(HardwareSetCouplingRequest)} (channel: {channelIndex}, coupling: {hardwareSetCouplingRequest.Coupling})");
                                            channelFrontend.Coupling = hardwareSetCouplingRequest.Coupling;
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
                        if (hardwareControl.Request.Reader.PeekAvailable() == 0)
                            Thread.Sleep(150);
                    }
                }

                try
                {
                    if (!validDataDto)
                    {
                        dataDto = hardwarePool.Return.Reader.Read(cancelToken);
                        dataDto.Memory.Reset();
                        validDataDto = true;
                    }
                    if (thunderscope.TryRead(dataDto.Memory, cancelToken))
                    {
                        if (enqueueCounter == 0)
                            logger.LogDebug("First block of data received");
                        periodicEnqueueCount++;
                        enqueueCounter++;
                        dataDto.MemoryType = dataType;
                        dataDto.HardwareConfig = thunderscope.GetConfiguration();
                        hardwarePool.Source.Writer.Write(dataDto, cancelToken);
                        validDataDto = false;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (ThunderscopeFifoOverflowException)
                {
                    logger.LogWarning("Scope had FIFO overflow - ignore and continue");
                }
                catch (ThunderscopeNotRunningException)
                {
                    // logger.LogWarning("Tried to read from stopped scope");
                }
                catch (Exception ex)
                {
                    if (ex.Message == "ReadFile - failed (1359)")
                    {
                        logger.LogError(ex, $"{nameof(HardwareThread)} error");
                    }
                    throw;
                }

                if (periodicUpdateTimer.ElapsedMilliseconds >= 10000)
                {
                    var oneSecondEnqueueCount = periodicEnqueueCount / periodicUpdateTimer.Elapsed.TotalSeconds;
                    logger.LogDebug($"[Stream] MB/sec: {(oneSecondEnqueueCount * ThunderscopeMemory.DataLength / 1000 / 1000):F3}, MiB/sec: {(oneSecondEnqueueCount * ThunderscopeMemory.DataLength / 1024 / 1024):F3}");

                    if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                    {
                        var status = liteXThunderscope.GetStatus();
                        logger.LogDebug($"[LiteX] lost buffers: {status.AdcSamplesLost}, temp: {status.FpgaTemp:F2}, VCC int: {status.VccInt:F3}, VCC aux: {status.VccAux:F3}, VCC BRAM: {status.VccBram:F3}, ADC Sync: {status.AdcFrameSync}");
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
