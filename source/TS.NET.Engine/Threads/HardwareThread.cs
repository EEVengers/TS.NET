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
            bool validDataDto = true;
            var resolution = AdcResolution.EightBit;
            var dataType = ThunderscopeDataType.I8;
            var segmentLengthBytes = ThunderscopeSettings.SegmentLengthBytes;
            var memory = new ThunderscopeMemory(ThunderscopeSettings.SegmentLengthBytes);
            bool warning = false;

            logger.LogDebug($"Block size: {ThunderscopeSettings.SegmentLengthBytes}");
#if DEBUG
            thunderscope.Start();
#endif

            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                while (hardwareControl.Request.Reader.TryRead(out var request))
                {
                    // Do configuration update, pausing acquisition if necessary
                    switch (request)
                    {
                        case HardwareStart hardwareStartRequest:
                            thunderscope.Start();
                            logger.LogDebug($"{nameof(HardwareStart)}");
                            break;
                        case HardwareStopRequest hardwareStopRequest:
                            thunderscope.Stop();
                            hardwareControl.Response.Writer.Write(new HardwareStopResponse());
                            logger.LogDebug($"{nameof(HardwareStopRequest)}");
                            break;
                        case HardwareSetRate hardwareSetRateRequest:
                            {
                                thunderscope.SetRate(hardwareSetRateRequest.Rate);
                                logger.LogDebug($"{nameof(HardwareSetRate)} (rate: {hardwareSetRateRequest.Rate})");
                                break;
                            }
                        case HardwareSetResolution hardwareSetResolutionRequest:
                            {
                                resolution = hardwareSetResolutionRequest.Resolution;
                                dataType = resolution switch
                                {
                                    AdcResolution.EightBit => ThunderscopeDataType.I8,
                                    AdcResolution.TwelveBit => ThunderscopeDataType.I16,
                                    _ => throw new NotImplementedException()
                                };
                                thunderscope.SetResolution(hardwareSetResolutionRequest.Resolution);
                                logger.LogDebug($"{nameof(HardwareSetResolution)} (resolution: {hardwareSetResolutionRequest.Resolution})");
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
                                hardwareControl.Response.Writer.Write(new HardwareGetTerminationResponse(frontend.RequestedTermination, frontend.ActualTermination));
                                logger.LogDebug($"{nameof(HardwareGetTerminationRequest)}");
                                break;
                            }
                        case HardwareSetEnabled hardwareSetEnabledRequest:
                            {
                                var channelIndex = ((HardwareSetEnabled)request).ChannelIndex;
                                logger.LogDebug($"{nameof(HardwareSetEnabled)} (channel: {channelIndex}, enabled: {hardwareSetEnabledRequest.Enabled})");
                                thunderscope.SetChannelEnable(channelIndex, hardwareSetEnabledRequest.Enabled);
                                break;
                            }
                        case HardwareSetChannelFrontendRequest hardwareConfigureChannelFrontendDto:
                            {
                                var channelIndex = ((HardwareSetChannelFrontendRequest)request).ChannelIndex;
                                var channelFrontend = thunderscope.GetChannelFrontend(channelIndex);
                                switch (request)
                                {
                                    case HardwareSetVoltOffset hardwareSetOffsetRequest:
                                        logger.LogDebug($"{nameof(HardwareSetVoltOffset)} (channel: {channelIndex}, offset: {hardwareSetOffsetRequest.VoltOffset})");
                                        channelFrontend.RequestedVoltOffset = hardwareSetOffsetRequest.VoltOffset;
                                        break;
                                    case HardwareSetVoltFullScale hardwareSetVdivRequest:
                                        logger.LogDebug($"{nameof(HardwareSetVoltFullScale)} (channel: {channelIndex}, scale: {hardwareSetVdivRequest.VoltFullScale})");
                                        channelFrontend.RequestedVoltFullScale = hardwareSetVdivRequest.VoltFullScale;
                                        break;
                                    case HardwareSetBandwidth hardwareSetBandwidthRequest:
                                        logger.LogDebug($"{nameof(HardwareSetBandwidth)} (channel: {channelIndex}, bandwidth: {hardwareSetBandwidthRequest.Bandwidth})");
                                        channelFrontend.Bandwidth = hardwareSetBandwidthRequest.Bandwidth;
                                        break;
                                    case HardwareSetCoupling hardwareSetCouplingRequest:
                                        logger.LogDebug($"{nameof(HardwareSetCoupling)} (channel: {channelIndex}, coupling: {hardwareSetCouplingRequest.Coupling})");
                                        channelFrontend.Coupling = hardwareSetCouplingRequest.Coupling;
                                        break;
                                    case HardwareSetTermination hardwareSetTerminationRequest:
                                        logger.LogDebug($"{nameof(HardwareSetTermination)} (channel: {channelIndex}, termination: {hardwareSetTerminationRequest.Termination})");
                                        channelFrontend.RequestedTermination = hardwareSetTerminationRequest.Termination;
                                        break;
                                    default:
                                        logger.LogWarning($"Unknown {nameof(HardwareSetChannelFrontendRequest)}: {request}");
                                        break;
                                }
                                thunderscope.SetChannelFrontend(channelIndex, channelFrontend);
                                break;
                            }
                        case HardwareSetChannelManualControl hardwareSetChannelManualControlRequest:
                            {
                                var channelIndex = hardwareSetChannelManualControlRequest.ChannelIndex;
                                var channel = hardwareSetChannelManualControlRequest.Channel;

                                ((Driver.Libtslitex.Thunderscope)thunderscope).SetChannelManualControl(channelIndex, channel);
                                break;
                            }
                        case HardwareSetAdcCalibration hardwareSetAdcCalibrationRequest:
                            {
                                ((Driver.Libtslitex.Thunderscope)thunderscope).SetAdcCalibration(hardwareSetAdcCalibrationRequest.AdcCalibration);
                                break;
                            }
                        default:
                            logger.LogWarning($"Unknown {nameof(HardwareRequestDto)}: {request}");
                            break;
                    }
                }

                try
                {
                    if (!validDataDto)
                    {
                        if(hardwarePool.Return.Reader.TryRead(out var newDataDto, 10, cancelToken))
                        {
                            dataDto = newDataDto;
                            validDataDto = true;
                        }
                        else
                        {
                            // If there's no pool available, continue processing the hardwareControl requests
                            logger.LogDebug("No pool data available");
                            continue;
                        }                       
                    }
                    if (thunderscope.TryRead(memory, out var hardwareConfig, out var sampleStartIndex, out var sampleLength))
                    {
                        if (enqueueCounter == 0)
                            logger.LogDebug("First block of data received");

                        switch (hardwareConfig.Acquisition.AdcChannelMode)
                        {
                            case AdcChannelMode.Single:
                                memory.DataSpanI8.CopyTo(dataDto.Memory.DataSpanI8);
                                break;
                            case AdcChannelMode.Dual:
                                switch (dataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        ShuffleI8.TwoChannels(input: memory.DataSpanI8, output: dataDto.Memory.DataSpanI8);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        if (!warning)
                                        {
                                            warning = true;
                                            logger.LogWarning("Unoptimised ShuffleI16.TwoChannels");
                                        }
                                        ShuffleI16.TwoChannels(input: memory.DataSpanI16, output: dataDto.Memory.DataSpanI16);
                                        break;
                                }
                                break;
                            case AdcChannelMode.Quad:
                                switch (dataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        ShuffleI8.FourChannels(input: memory.DataSpanI8, output: dataDto.Memory.DataSpanI8);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        if (!warning)
                                        {
                                            warning = true;
                                            logger.LogWarning("Unoptimised ShuffleI16.FourChannels");
                                        }
                                        ShuffleI16.FourChannels(input: memory.DataSpanI16, output: dataDto.Memory.DataSpanI16);
                                        break;
                                }
                                break;
                        }

                        periodicEnqueueCount++;
                        enqueueCounter++;
                        dataDto.SampleStartIndex = sampleStartIndex;
                        dataDto.SampleLength = sampleLength;
                        dataDto.MemoryType = dataType;
                        dataDto.HardwareConfig = hardwareConfig;
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
                    logger.LogDebug($"[Stream] MB/sec: {(oneSecondEnqueueCount * segmentLengthBytes / 1000 / 1000):F3}, MiB/sec: {(oneSecondEnqueueCount * segmentLengthBytes / 1024 / 1024):F3}");

                    if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                    {
                        var status = liteXThunderscope.GetStatus();
                        logger.LogDebug($"[LiteX] lost buffers: {status.AdcSamplesLost}, temp: {status.FpgaTemp:F2}, VCC int: {status.VccInt:F3}, VCC aux: {status.VccAux:F3}, VCC BRAM: {status.VccBram:F3}, ADC Sync: {status.AdcFrameSync}");
                    }

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
