using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics;

namespace TS.NET.Engine;

public class ProcessingThread : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly IThunderscope thunderscope;
    private readonly BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl;
    private readonly BlockingChannelWriter<INotificationDto>? uiNotifications;
    private readonly CaptureCircularBuffer captureBuffer;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskLoop;

    public ProcessingThread(
        ILogger logger,
        ThunderscopeSettings settings,
        IThunderscope thunderscope,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        BlockingChannelWriter<INotificationDto>? uiNotifications,
        CaptureCircularBuffer captureBuffer)
    {
        this.logger = logger;
        this.settings = settings;
        this.thunderscope = thunderscope;
        this.processingControl = processingControl;
        this.uiNotifications = uiNotifications;
        this.captureBuffer = captureBuffer;
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        cancelTokenSource = new CancellationTokenSource();
        taskLoop = Task.Factory.StartNew(() => Loop(
            logger: logger,
            settings: settings,
            thunderscope: thunderscope,
            processingControl: processingControl,
            uiNotifications: uiNotifications,
            captureBuffer: captureBuffer,
            startSemaphore: startSemaphore,
            cancelToken: cancelTokenSource.Token), TaskCreationOptions.LongRunning);
    }

    public void Stop()
    {
        cancelTokenSource?.Cancel();
        taskLoop?.Wait();
    }

    private static unsafe void Loop(
        ILogger logger,
        ThunderscopeSettings settings,
        IThunderscope thunderscope,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        BlockingChannelWriter<INotificationDto>? uiNotifications,
        CaptureCircularBuffer captureBuffer,
        SemaphoreSlim startSemaphore,
        CancellationToken cancelToken)
    {
        try
        {
            Thread.CurrentThread.Name = "Processing";
            if (settings.ProcessingThreadProcessorAffinity > -1)
            {
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    Thread.BeginThreadAffinity();
                    OsThread.SetThreadAffinity(settings.ProcessingThreadProcessorAffinity);
                    logger.LogDebug($"{nameof(ProcessingThread)} processor affinity set to {settings.ProcessingThreadProcessorAffinity}");
                }
            }

            //ThunderscopeBridgeConfig bridgeConfig = new()
            //{
            //    MaxChannelCount = settings.MaxChannelCount,
            //    MaxChannelDataLength = settings.MaxChannelDataLength,
            //    MaxDataRegionDataByteWidth = ThunderscopeDataType.I8.ByteWidth(),
            //    DataRegionCount = 2
            //};
            //ThunderscopeDataBridgeWriter bridge = new(bridgeNamespace, settings.MaxChannelCount * settings.MaxChannelDataLength * ThunderscopeDataType.I8.ByteWidth());

            var initialHardwareConfig = thunderscope.GetConfiguration();

            // Set some sensible defaults
            ushort initialChannelCount = initialHardwareConfig.Acquisition.AdcChannelMode switch
            {
                AdcChannelMode.Quad => 4,
                AdcChannelMode.Dual => 2,
                AdcChannelMode.Single => 1,
                _ => throw new NotImplementedException()
            };
            var initialChannelDataType = initialHardwareConfig.Acquisition.Resolution switch
            {
                AdcResolution.EightBit => ThunderscopeDataType.I8,
                AdcResolution.TwelveBit => ThunderscopeDataType.I16,
                _ => throw new NotImplementedException()
            };
            var processingConfig = new ThunderscopeProcessingConfig
            {
                //AdcResolution = initialHardwareConfig.Acquisition.Resolution,
                //SampleRateHz = 1_000_000_000,
                //EnabledChannels = initialHardwareConfig.Acquisition.EnabledChannels,
                ChannelDataLength = 1000,
                ChannelDataType = initialChannelDataType,
                Mode = Mode.Normal,     // Temporary, change back to AUTO when NotImplementedException fixed
                TriggerChannel = TriggerChannel.Channel1,
                TriggerType = TriggerType.Edge,
                TriggerDelayFs = (ulong)(1e15 / (1e9 / initialChannelCount) * 500),   // Set the trigger delay to the middle of the capture
                TriggerHoldoffFs = 0,
                TriggerInterpolation = true,
                AutoTimeoutMs = 1000,
                EdgeTriggerParameters = new EdgeTriggerParameters() { LevelV = 0, HysteresisPercent = 5, Direction = EdgeDirection.Rising },
                BurstTriggerParameters = new BurstTriggerParameters() { WindowHighLevel = 64, WindowLowLevel = -64, MinimumInRangePeriod = 450000 },
                BoxcarAveraging = BoxcarAveraging.None
            };
            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
            uiNotifications?.TryWrite(new ProcessingStop());
            var currentHardwareConfig = initialHardwareConfig;

            // Periodic debug display variables
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            ulong totalReadCount = 0;
            uint periodicReadCount = 0;

            Stopwatch periodicUpdateTimer = Stopwatch.StartNew();

            var acquisitionBuffer = new AcquisitionCircularBuffer(settings.MaxCaptureLength, ThunderscopeSettings.SegmentLengthBytes, ThunderscopeDataType.I16);

            captureBuffer.Configure(initialChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);

            // Triggering:
            // There are 3 states for Trigger Mode: normal, single, auto.
            // (these only run during Start, not during Stop. Invoking Force will ignore Start/Stop.)
            // Normal: wait for trigger indefinately and run continuously.
            // Single: wait for trigger indefinately and then stop.
            // Auto: wait for trigger indefinately, push update when timeout occurs, and run continously.
            //
            // runTrigger: enables/disables trigger subsystem. 
            // forceTriggerLatch: disregards the Trigger Mode, push update immediately and set forceTrigger to false. If a standard trigger happened at the same time as a force, the force is ignored so the bridge only updates once.
            // singleTriggerLatch: used in Single mode to stop the trigger subsystem after a trigger.

            ITriggerI8 triggerI8 = null;
            ITriggerI16 triggerI16 = null;
            ResetTrigger();

            EdgeTriggerResults edgeTriggerResults = new EdgeTriggerResults()
            {
                ArmIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000],         // 1000 samples is the minimum window width
                TriggerIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000],     // 1000 samples is the minimum window width
                CaptureEndIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000]   // 1000 samples is the minimum window width
            };
            bool runMode = false;
            bool forceTriggerLatch = false;
            bool singleTriggerLatch = false;    // "Latch" because it will reset state back to false. When reset, runTrigger will be set to false.
            Mode modeAfterForce = processingConfig.Mode;

            // Variables for Auto triggering
            Stopwatch autoTimeoutTimer = Stopwatch.StartNew();

            var preShuffleMemory = new ThunderscopeMemory(ThunderscopeSettings.SegmentLengthBytes);
            var postShuffleMemory = new ThunderscopeMemory(ThunderscopeSettings.SegmentLengthBytes);
            bool optimisationWarning = false;

            logger.LogInformation("Started");
            startSemaphore.Release();

#if DEBUG
            thunderscope.Start();
#endif

            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                while (processingControl.Request.Reader.TryRead(out var request))
                {
                    switch (request)
                    {
                        case HardwareSetRate hardwareSetRate:
                            if (currentHardwareConfig.Acquisition.SampleRateHz != hardwareSetRate.Rate)
                            {
                                currentHardwareConfig.Acquisition.SampleRateHz = hardwareSetRate.Rate;
                                UpdateRateAndCoerce(forceRateUpdate: true);
                                currentHardwareConfig = thunderscope.GetConfiguration();
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(HardwareSetRate)} ({currentHardwareConfig.Acquisition.SampleRateHz})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(HardwareSetRate)} (no change)");
                            }
                            break;
                        case HardwareSetResolution hardwareSetResolution:
                            if (currentHardwareConfig.Acquisition.Resolution != hardwareSetResolution.Resolution)
                            {
                                currentHardwareConfig.Acquisition.Resolution = hardwareSetResolution.Resolution;
                                processingConfig.ChannelDataType = hardwareSetResolution.Resolution switch
                                {
                                    AdcResolution.EightBit => ThunderscopeDataType.I8,
                                    AdcResolution.TwelveBit => ThunderscopeDataType.I16,
                                    _ => throw new NotImplementedException()
                                };

                                UpdateRateAndCoerce(forceRateUpdate: false);
                                thunderscope.SetResolution(hardwareSetResolution.Resolution);
                                currentHardwareConfig = thunderscope.GetConfiguration();
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(HardwareSetResolution)} ({hardwareSetResolution.Resolution})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(HardwareSetResolution)} (no change)");
                            }
                            break;
                        case HardwareSetChannelEnabled processingSetEnabled:
                            var enabledChannels = CalculateChannelMask(currentHardwareConfig.Acquisition.EnabledChannels, processingSetEnabled.ChannelIndex, processingSetEnabled.Enabled);
                            if (currentHardwareConfig.Acquisition.EnabledChannels != enabledChannels)
                            {
                                UpdateRateAndCoerce(forceRateUpdate: false);
                                thunderscope.SetChannelEnable(processingSetEnabled.ChannelIndex, processingSetEnabled.Enabled);
                                currentHardwareConfig = thunderscope.GetConfiguration();
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(HardwareSetChannelEnabled)} ({processingSetEnabled.ChannelIndex} {processingSetEnabled.Enabled})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(HardwareSetChannelEnabled)} (no change)");
                            }
                            break;
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
                                currentHardwareConfig = thunderscope.GetConfiguration();
                                break;
                            }

                        case HardwareGetRateRequest hardwareGetRateRequest:
                            processingControl.Response.Writer.Write(new HardwareGetRateResponse(currentHardwareConfig.Acquisition.SampleRateHz));
                            logger.LogDebug($"{nameof(HardwareGetRateRequest)}");
                            break;
                        case HardwareGetResolutionRequest hardwareGetResolutionRequest:
                            processingControl.Response.Writer.Write(new HardwareGetResolutionResponse(currentHardwareConfig.Acquisition.Resolution));
                            logger.LogDebug($"{nameof(HardwareGetResolutionRequest)}");
                            break;
                        case HardwareGetEnabledRequest hardwareGetEnabledRequest:
                            processingControl.Response.Writer.Write(new HardwareGetEnabledResponse(currentHardwareConfig.Acquisition.EnabledChannels));
                            logger.LogDebug($"{nameof(HardwareGetEnabledRequest)}");
                            break;
                        case HardwareGetChannelFrontendRequest hardwareGetChannelFrontendRequest:
                            {
                                var channelIndex = ((HardwareSetChannelFrontendRequest)request).ChannelIndex;
                                var channelFrontend = thunderscope.GetChannelFrontend(channelIndex);
                                currentHardwareConfig.Frontend[channelIndex] = channelFrontend;
                                switch (request)
                                {
                                    case HardwareGetVoltOffsetRequest hardwareGetVoltOffsetRequest:
                                        {
                                            logger.LogDebug($"{nameof(HardwareGetVoltOffsetRequest)}");
                                            processingControl.Response.Writer.Write(new HardwareGetVoltOffsetResponse(channelFrontend.RequestedVoltOffset, channelFrontend.ActualVoltOffset));
                                            break;
                                        }
                                    case HardwareGetVoltFullScaleRequest hardwareGetVoltFullScaleRequest:
                                        {
                                            logger.LogDebug($"{nameof(HardwareGetVoltFullScaleRequest)}");
                                            processingControl.Response.Writer.Write(new HardwareGetVoltFullScaleResponse(channelFrontend.RequestedVoltFullScale, channelFrontend.ActualVoltFullScale));
                                            break;
                                        }
                                    case HardwareGetBandwidthRequest hardwareGetBandwidthRequest:
                                        {
                                            logger.LogDebug($"{nameof(HardwareGetBandwidthRequest)}");
                                            processingControl.Response.Writer.Write(new HardwareGetBandwidthResponse(channelFrontend.Bandwidth));
                                            break;
                                        }
                                    case HardwareGetCouplingRequest hardwareGetCouplingRequest:
                                        {
                                            logger.LogDebug($"{nameof(HardwareGetCouplingRequest)}");
                                            processingControl.Response.Writer.Write(new HardwareGetCouplingResponse(channelFrontend.Coupling));
                                            break;
                                        }
                                    case HardwareGetTerminationRequest hardwareGetTerminationRequest:
                                        {
                                            logger.LogDebug($"{nameof(HardwareGetTerminationRequest)}");
                                            processingControl.Response.Writer.Write(new HardwareGetTerminationResponse(channelFrontend.RequestedTermination, channelFrontend.ActualTermination));
                                            break;
                                        }
                                }
                                break;
                            }

                        case ProcessingRun processingRun:
                            ResetAll();
                            if (processingConfig.Mode == Mode.Single)
                                singleTriggerLatch = true;
                            runMode = true;
                            thunderscope.Start();
                            uiNotifications?.TryWrite(processingRun);
                            logger.LogDebug($"{nameof(ProcessingRun)}");
                            break;
                        case ProcessingStop processingStop:
                            runMode = false;
                            forceTriggerLatch = false;
                            thunderscope.Stop();
                            uiNotifications?.TryWrite(processingStop);
                            logger.LogDebug($"{nameof(ProcessingStop)}");
                            break;
                        case ProcessingForce processingForce:
                            if (runMode)        // FORCE is ignored if not in runMode.
                            {
                                modeAfterForce = processingConfig.Mode;
                                forceTriggerLatch = true;
                                uiNotifications?.TryWrite(processingForce);
                                logger.LogDebug($"{nameof(ProcessingForce)}");
                            }
                            break;

                        case ProcessingSetMode processingSetMode:
                            singleTriggerLatch = false;
                            switch (processingSetMode.Mode)
                            {
                                case Mode.Normal:                // NORMAL/STREAM/AUTO use RUN/STOP on user demand
                                case Mode.Stream:
                                    processingConfig.Mode = processingSetMode.Mode;
                                    break;
                                case Mode.Auto:
                                    autoTimeoutTimer.Restart();
                                    processingConfig.Mode = processingSetMode.Mode;
                                    break;
                                case Mode.Single:                // SINGLE forces runMode.
                                    if (runMode != true)
                                    {
                                        runMode = true;
                                        thunderscope.Start();
                                    }
                                    singleTriggerLatch = true;
                                    processingConfig.Mode = processingSetMode.Mode;
                                    break;
                            }
                            ResetAll();
                            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                            logger.LogDebug($"{nameof(ProcessingSetMode)} (mode: {processingConfig.Mode})");
                            break;
                        case ProcessingSetDepth processingSetDepth:
                            if (processingConfig.ChannelDataLength != processingSetDepth.Samples)
                            {
                                processingConfig.ChannelDataLength = processingSetDepth.Samples;
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetDepth)} ({processingConfig.ChannelDataLength})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetDepth)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerSource processingSetTriggerSourceDto:
                            if (processingConfig.TriggerChannel != processingSetTriggerSourceDto.Channel)
                            {
                                processingConfig.TriggerChannel = processingSetTriggerSourceDto.Channel;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSource)} (channel: {processingConfig.TriggerChannel})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSource)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerType processingSetTriggerTypeDto:
                            if (processingConfig.TriggerType != processingSetTriggerTypeDto.Type)
                            {
                                processingConfig.TriggerType = processingSetTriggerTypeDto.Type;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerType)} (type: {processingConfig.TriggerType})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerType)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerDelay processingSetTriggerDelayDto:
                            if (processingConfig.TriggerDelayFs != processingSetTriggerDelayDto.Femtoseconds)
                            {
                                processingConfig.TriggerDelayFs = processingSetTriggerDelayDto.Femtoseconds;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelay)} (femtoseconds: {processingConfig.TriggerDelayFs})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelay)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerHoldoff processingSetTriggerHoldoffDto:
                            if (processingConfig.TriggerHoldoffFs != processingSetTriggerHoldoffDto.Femtoseconds)
                            {
                                processingConfig.TriggerHoldoffFs = processingSetTriggerHoldoffDto.Femtoseconds;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerHoldoff)} (femtoseconds: {processingConfig.TriggerHoldoffFs})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelay)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerInterpolation processingSetTriggerInterpolation:
                            if (processingConfig.TriggerInterpolation != processingSetTriggerInterpolation.Enabled)
                            {
                                processingConfig.TriggerInterpolation = processingSetTriggerInterpolation.Enabled;
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerInterpolation)} (enabled: {processingSetTriggerInterpolation.Enabled})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerInterpolation)} (no change)");
                            }
                            break;
                        case ProcessingSetEdgeTriggerLevel processingSetTriggerLevelDto:
                            var requestedTriggerLevel = processingSetTriggerLevelDto.LevelVolts;
                            if (requestedTriggerLevel != processingConfig.EdgeTriggerParameters.LevelV)
                            {
                                processingConfig.EdgeTriggerParameters.LevelV = requestedTriggerLevel;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerLevel)} (level: {processingConfig.EdgeTriggerParameters.LevelV}, hysteresis %: {processingConfig.EdgeTriggerParameters.HysteresisPercent})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerLevel)} (no change)");
                            }
                            break;
                        case ProcessingSetEdgeTriggerDirection processingSetEdgeTriggerDirection:
                            if (processingConfig.EdgeTriggerParameters.Direction != processingSetEdgeTriggerDirection.Edge)
                            {
                                processingConfig.EdgeTriggerParameters.Direction = processingSetEdgeTriggerDirection.Edge;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerDirection)} (direction: {processingSetEdgeTriggerDirection.Edge})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerDirection)} (no change)");
                            }
                            break;
                        
                        case ProcessingGetStateRequest processingGetStateRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetStateResponse(runMode));
                            logger.LogDebug($"{nameof(ProcessingGetStateRequest)}");
                            break;
                        case ProcessingGetModeRequest processingGetModeRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetModeResponse(processingConfig.Mode));
                            logger.LogDebug($"{nameof(ProcessingGetModeRequest)}");
                            break;
                        case ProcessingGetDepthRequest processingGetDepthRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetDepthResponse(processingConfig.ChannelDataLength));
                            logger.LogDebug($"{nameof(ProcessingGetDepthRequest)}");
                            break;
                        case ProcessingGetTriggerSourceRequest processingGetTriggerSourceRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetTriggerSourceResponse(processingConfig.TriggerChannel));
                            logger.LogDebug($"{nameof(ProcessingGetTriggerSourceRequest)}");
                            break;
                        case ProcessingGetTriggerTypeRequest processingGetTriggerTypeRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetTriggerTypeResponse(processingConfig.TriggerType));
                            logger.LogDebug($"{nameof(ProcessingGetTriggerTypeRequest)}");
                            break;
                        case ProcessingGetTriggerDelayRequest processingGetTriggerDelayRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetTriggerDelayResponse(processingConfig.TriggerDelayFs));
                            logger.LogDebug($"{nameof(ProcessingGetTriggerDelayRequest)}");
                            break;
                        case ProcessingGetTriggerHoldoffRequest processingGetTriggerHoldoffRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetTriggerHoldoffResponse(processingConfig.TriggerHoldoffFs));
                            logger.LogDebug($"{nameof(ProcessingGetTriggerHoldoffRequest)}");
                            break;
                        case ProcessingGetTriggerInterpolationRequest processingGetTriggerInterpolationRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetTriggerInterpolationResponse(processingConfig.TriggerInterpolation));
                            logger.LogDebug($"{nameof(ProcessingGetTriggerInterpolationRequest)}");
                            break;
                        case ProcessingGetEdgeTriggerLevelRequest processingGetEdgeTriggerLevelRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetEdgeTriggerLevelResponse(processingConfig.EdgeTriggerParameters.LevelV));
                            logger.LogDebug($"{nameof(ProcessingGetEdgeTriggerLevelRequest)}");
                            break;
                        case ProcessingGetEdgeTriggerDirectionRequest processingGetEdgeTriggerDirectionRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetEdgeTriggerDirectionResponse(processingConfig.EdgeTriggerParameters.Direction));
                            logger.LogDebug($"{nameof(ProcessingGetEdgeTriggerDirectionRequest)}");
                            break;

                        case ProcessingGetRatesRequest processingGetRatesRequest:
                            {
                                logger.LogDebug("===========Processing control request");
                                logger.LogDebug($"{nameof(ProcessingGetRatesRequest)}");
                                List<ulong> rates = [];
                                switch (settings.HardwareDriver.ToLower())
                                {
                                    case "litex":
                                    case "libtslitex":
                                        {
                                            switch (BitOperations.PopCount(currentHardwareConfig.Acquisition.EnabledChannels))
                                            {
                                                case 1:
                                                    if (currentHardwareConfig.Acquisition.Resolution == AdcResolution.EightBit)
                                                        rates.Add(1_000_000_000);
                                                    rates.Add(660_000_000);
                                                    rates.Add(500_000_000);
                                                    rates.Add(330_000_000);
                                                    rates.Add(250_000_000);
                                                    rates.Add(165_000_000);
                                                    rates.Add(100_000_000);
                                                    break;
                                                case 2:
                                                    if (currentHardwareConfig.Acquisition.Resolution == AdcResolution.EightBit)
                                                        rates.Add(500_000_000);
                                                    rates.Add(330_000_000);
                                                    rates.Add(250_000_000);
                                                    rates.Add(165_000_000);
                                                    rates.Add(100_000_000);
                                                    break;
                                                case 3:
                                                case 4:
                                                    if (currentHardwareConfig.Acquisition.Resolution == AdcResolution.EightBit)
                                                        rates.Add(250_000_000);
                                                    rates.Add(165_000_000);
                                                    rates.Add(100_000_000);
                                                    break;
                                            }
                                            break;
                                        }
                                    case "simulation":
                                        {
                                            rates.Add(1000000000);
                                            break;
                                        }
                                }
                                processingControl.Response.Writer.Write(new ProcessingGetRatesResponse(rates.ToArray()));
                                logger.LogDebug($"{nameof(ProcessingGetRatesResponse)}");
                                break;
                            }
                        default:
                            logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                            break;
                    }
                }

                if (thunderscope.Running())
                {
                    if (thunderscope.TryRead(preShuffleMemory, out var sampleStartIndex, out var sampleLength))
                    {
                        totalReadCount++;
                        periodicReadCount++;

                        // To do: decide if the "Shuffle" and "Write to acquisition buffers" regions should move inside the `if (runMode) { }`
                        // Shuffle
                        switch (currentHardwareConfig.Acquisition.AdcChannelMode)
                        {
                            case AdcChannelMode.Single:
                                preShuffleMemory.DataSpanI8.CopyTo(postShuffleMemory.DataSpanI8);
                                break;
                            case AdcChannelMode.Dual:
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        ShuffleI8.TwoChannels(input: preShuffleMemory.DataSpanI8, output: postShuffleMemory.DataSpanI8);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        if (!optimisationWarning)
                                        {
                                            optimisationWarning = true;
                                            logger.LogWarning("Unoptimised ShuffleI16.TwoChannels");
                                        }
                                        ShuffleI16.TwoChannels(input: preShuffleMemory.DataSpanI16, output: postShuffleMemory.DataSpanI16);
                                        break;
                                }
                                break;
                            case AdcChannelMode.Quad:
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        ShuffleI8.FourChannels(input: preShuffleMemory.DataSpanI8, output: postShuffleMemory.DataSpanI8);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        if (!optimisationWarning)
                                        {
                                            optimisationWarning = true;
                                            logger.LogWarning("Unoptimised ShuffleI16.FourChannels");
                                        }
                                        ShuffleI16.FourChannels(input: preShuffleMemory.DataSpanI16, output: postShuffleMemory.DataSpanI16);
                                        break;
                                }
                                break;
                        }

                        // Write to acquisition buffers
                        switch (currentHardwareConfig.Acquisition.AdcChannelMode)
                        {
                            case AdcChannelMode.Single:
                                // Write to circular sample buffer
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        acquisitionBuffer.Write1Channel<sbyte>(postShuffleMemory.DataSpanI8, sampleStartIndex);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        acquisitionBuffer.Write1Channel<short>(postShuffleMemory.DataSpanI16, sampleStartIndex);
                                        break;
                                }
                                break;
                            case AdcChannelMode.Dual:
                                // Write to circular sample buffer
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        {
                                            var span = postShuffleMemory.DataSpanI8;
                                            acquisitionBuffer.Write2Channel<sbyte>(Span2Ch(0, span), Span2Ch(1, span), sampleStartIndex);
                                            break;
                                        }
                                    case ThunderscopeDataType.I16:
                                        {
                                            var span = postShuffleMemory.DataSpanI16;
                                            acquisitionBuffer.Write2Channel<short>(Span2Ch(0, span), Span2Ch(1, span), sampleStartIndex);
                                            break;
                                        }
                                }
                                break;
                            case AdcChannelMode.Quad:
                                // Write to circular sample buffer
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        {
                                            var span = postShuffleMemory.DataSpanI8;
                                            acquisitionBuffer.Write4Channel<sbyte>(Span4Ch(0, span), Span4Ch(1, span), Span4Ch(2, span), Span4Ch(3, span), sampleStartIndex);
                                            break;
                                        }
                                    case ThunderscopeDataType.I16:
                                        {
                                            var span = postShuffleMemory.DataSpanI16;
                                            acquisitionBuffer.Write4Channel<short>(Span4Ch(0, span), Span4Ch(1, span), Span4Ch(2, span), Span4Ch(3, span), sampleStartIndex);
                                            break;
                                        }
                                }
                                break;
                        }

                        // Trigger processing
                        if (runMode)
                        {
                            switch (processingConfig.Mode)
                            {
                                case Mode.Normal:
                                case Mode.Single:
                                case Mode.Auto:
                                    if (forceTriggerLatch)
                                    {
                                        // If FORCE, don't do trigger processing until the FORCE capture is complete.
                                        // This allows a sequence of commands for "immediate unconditional single capture" UI button:
                                        //    STOP
                                        //    TRIG:SOURCE NONE
                                        //    SINGLE
                                        //    FORCE
                                        //    TRIG:SOURCE 1/2/3/4
                                        if (acquisitionBuffer.SamplesInBuffer >= processingConfig.ChannelDataLength)
                                        {
                                            Capture(triggered: false, triggerChannelCaptureIndex: 0, captureEndIndex: sampleStartIndex + (ulong)sampleLength);
                                            forceTriggerLatch = false;
                                            autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened

                                            if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                            {
                                                singleTriggerLatch = false;
                                                runMode = false;
                                                forceTriggerLatch = false;
                                                thunderscope.Stop();
                                                break;
                                            }
                                        }
                                    }
                                    else if (currentHardwareConfig.Acquisition.IsTriggerChannelAnEnabledChannel(processingConfig.TriggerChannel))
                                    {
                                        Span<sbyte> triggerChannelBufferI8;
                                        Span<short> triggerChannelBufferI16;
                                        int triggerChannelCaptureIndex;
                                        switch (currentHardwareConfig.Acquisition.AdcChannelMode)
                                        {
                                            case AdcChannelMode.Single:
                                                triggerChannelCaptureIndex = 0;
                                                triggerChannelBufferI8 = postShuffleMemory.DataSpanI8;
                                                triggerChannelBufferI16 = postShuffleMemory.DataSpanI16;
                                                break;
                                            case AdcChannelMode.Dual:
                                                triggerChannelCaptureIndex = currentHardwareConfig.Acquisition.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                                triggerChannelBufferI8 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => Span2Ch(0, postShuffleMemory.DataSpanI8),
                                                    1 => Span2Ch(1, postShuffleMemory.DataSpanI8),
                                                    _ => throw new NotImplementedException()
                                                };
                                                triggerChannelBufferI16 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => Span2Ch(0, postShuffleMemory.DataSpanI16),
                                                    1 => Span2Ch(1, postShuffleMemory.DataSpanI16),
                                                    _ => throw new NotImplementedException()
                                                };
                                                break;
                                            case AdcChannelMode.Quad:
                                                triggerChannelCaptureIndex = currentHardwareConfig.Acquisition.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                                triggerChannelBufferI8 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => Span4Ch(0, postShuffleMemory.DataSpanI8),
                                                    1 => Span4Ch(1, postShuffleMemory.DataSpanI8),
                                                    2 => Span4Ch(2, postShuffleMemory.DataSpanI8),
                                                    3 => Span4Ch(3, postShuffleMemory.DataSpanI8),
                                                    _ => throw new NotImplementedException()
                                                };
                                                triggerChannelBufferI16 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => Span4Ch(0, postShuffleMemory.DataSpanI16),
                                                    1 => Span4Ch(1, postShuffleMemory.DataSpanI16),
                                                    2 => Span4Ch(2, postShuffleMemory.DataSpanI16),
                                                    3 => Span4Ch(3, postShuffleMemory.DataSpanI16),
                                                    _ => throw new NotImplementedException()
                                                };
                                                break;
                                            default:
                                                throw new NotImplementedException();
                                        }

                                        switch (processingConfig.ChannelDataType)
                                        {
                                            case ThunderscopeDataType.I8:
                                                triggerI8.Process(input: triggerChannelBufferI8, sampleStartIndex, ref edgeTriggerResults);
                                                break;
                                            case ThunderscopeDataType.I16:
                                                triggerI16.Process(input: triggerChannelBufferI16, sampleStartIndex, ref edgeTriggerResults);
                                                break;
                                        }

                                        if (edgeTriggerResults.CaptureEndCount > 0)
                                        {
                                            for (int i = 0; i < edgeTriggerResults.CaptureEndCount; i++)
                                            {
                                                Capture(triggered: true, triggerChannelCaptureIndex, edgeTriggerResults.CaptureEndIndices[i]);

                                                if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                                {
                                                    singleTriggerLatch = false;
                                                    runMode = false;
                                                    forceTriggerLatch = false;
                                                    thunderscope.Stop();
                                                    break;
                                                }
                                            }
                                            autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened
                                        }
                                    }
                                    else if (processingConfig.Mode == Mode.Auto && autoTimeoutTimer.ElapsedMilliseconds > processingConfig.AutoTimeoutMs)
                                    {
                                        StreamCapture();
                                    }
                                    break;
                                case Mode.Stream:
                                    StreamCapture();
                                    break;
                            }
                        }

                        // Debug information
                        var elapsedTime = periodicUpdateTimer.Elapsed.TotalSeconds;
                        if (elapsedTime >= 10)
                        {
                            var segmentLengthBytes = ThunderscopeSettings.SegmentLengthBytes;
                            var oneSecondEnqueueCount = periodicReadCount / periodicUpdateTimer.Elapsed.TotalSeconds;
                            logger.LogDebug($"[Stream] MB/sec: {(oneSecondEnqueueCount * segmentLengthBytes / 1000 / 1000):F3}, MiB/sec: {(oneSecondEnqueueCount * segmentLengthBytes / 1024 / 1024):F3}");

                            if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                            {
                                var status = liteXThunderscope.GetStatus();
                                logger.LogDebug($"[LiteX] lost buffers: {status.AdcSamplesLost}, temp: {status.FpgaTemp:F2}, VCC int: {status.VccInt:F3}, VCC aux: {status.VccAux:F3}, VCC BRAM: {status.VccBram:F3}, ADC Sync: {status.AdcFrameSync}");
                            }

                            var intervalCaptureTotal = captureBuffer.IntervalCaptureTotal;
                            var intervalCaptureDrops = captureBuffer.IntervalCaptureDrops;
                            var intervalCaptureReads = captureBuffer.IntervalCaptureReads;

                            logger.LogDebug($"[Capture stats] total/s: {intervalCaptureTotal / elapsedTime:F2}, drops/s: {intervalCaptureDrops / elapsedTime:F2}, UI reads/s: {intervalCaptureReads / elapsedTime:F2}");
                            logger.LogDebug($"[Capture buffer] capacity: {captureBuffer.MaxCaptureCount}, current: {captureBuffer.CurrentCaptureCount}, channel count: {captureBuffer.ChannelCount}, total: {captureBuffer.CaptureTotal}, drops: {captureBuffer.CaptureDrops}, reads: {captureBuffer.CaptureReads}");
                            periodicUpdateTimer.Restart();

                            periodicReadCount = 0;
                            captureBuffer.ResetIntervalStats();
                        }
                    }
                }
            }

            // Locally scoped methods for deduplication
            void Capture(bool triggered, int triggerChannelCaptureIndex, ulong captureEndIndex)
            {
                if (captureBuffer.TryStartWrite())
                {
                    int channelCount = currentHardwareConfig.Acquisition.EnabledChannelsCount();
                    switch (processingConfig.ChannelDataType)
                    {
                        case ThunderscopeDataType.I8:
                            {
                                switch (channelCount)
                                {
                                    case 1:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<sbyte>(0);
                                            acquisitionBuffer.Read1Channel(buffer1, captureEndIndex);
                                        }
                                        break;
                                    case 2:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<sbyte>(0);
                                            var buffer2 = captureBuffer.GetChannelWriteBuffer<sbyte>(1);
                                            acquisitionBuffer.Read2Channel(buffer1, buffer2, captureEndIndex);
                                        }
                                        break;
                                    case 3:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<sbyte>(0);
                                            var buffer2 = captureBuffer.GetChannelWriteBuffer<sbyte>(1);
                                            var buffer3 = captureBuffer.GetChannelWriteBuffer<sbyte>(2);
                                            acquisitionBuffer.Read3Channel(buffer1, buffer2, buffer3, captureEndIndex);
                                        }
                                        break;
                                    case 4:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<sbyte>(0);
                                            var buffer2 = captureBuffer.GetChannelWriteBuffer<sbyte>(1);
                                            var buffer3 = captureBuffer.GetChannelWriteBuffer<sbyte>(2);
                                            var buffer4 = captureBuffer.GetChannelWriteBuffer<sbyte>(3);
                                            acquisitionBuffer.Read4Channel(buffer1, buffer2, buffer3, buffer4, captureEndIndex);
                                        }
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                            break;
                        case ThunderscopeDataType.I16:
                            {
                                switch (channelCount)
                                {
                                    case 1:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<short>(0);
                                            acquisitionBuffer.Read1Channel(buffer1, captureEndIndex);
                                        }
                                        break;
                                    case 2:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<short>(0);
                                            var buffer2 = captureBuffer.GetChannelWriteBuffer<short>(1);
                                            acquisitionBuffer.Read2Channel(buffer1, buffer2, captureEndIndex);
                                        }
                                        break;
                                    case 3:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<short>(0);
                                            var buffer2 = captureBuffer.GetChannelWriteBuffer<short>(1);
                                            var buffer3 = captureBuffer.GetChannelWriteBuffer<short>(2);
                                            acquisitionBuffer.Read3Channel(buffer1, buffer2, buffer3, captureEndIndex);
                                        }
                                        break;
                                    case 4:
                                        {
                                            var buffer1 = captureBuffer.GetChannelWriteBuffer<short>(0);
                                            var buffer2 = captureBuffer.GetChannelWriteBuffer<short>(1);
                                            var buffer3 = captureBuffer.GetChannelWriteBuffer<short>(2);
                                            var buffer4 = captureBuffer.GetChannelWriteBuffer<short>(3);
                                            acquisitionBuffer.Read4Channel(buffer1, buffer2, buffer3, buffer4, captureEndIndex);
                                        }
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                            break;
                    }

                    var captureMetadata = new CaptureMetadata
                    {
                        Triggered = triggered,
                        TriggerChannelCaptureIndex = triggerChannelCaptureIndex,
                        HardwareConfig = currentHardwareConfig,
                        ProcessingConfig = processingConfig
                    };
                    captureBuffer.FinishWrite(captureMetadata);
                }
            }

            void StreamCapture()
            {
                int channelLength = processingConfig.ChannelDataLength;
                while (acquisitionBuffer.SamplesInBuffer > channelLength)
                {
                    throw new NotImplementedException();    // Need to figure out captureEndIndex below
                    Capture(triggered: false, triggerChannelCaptureIndex: 0, captureEndIndex: 0);
                }
            }

            Span<T> Span2Ch<T>(int channelIndex, Span<T> data)
            {
                return channelIndex switch
                {
                    0 => data.Slice(0, data.Length / 2),
                    1 => data.Slice(data.Length / 2, data.Length / 2),
                    _ => throw new InvalidDataException()
                };
            }

            Span<T> Span4Ch<T>(int channelIndex, Span<T> data)
            {
                return channelIndex switch
                {
                    0 => data.Slice(0, data.Length / 4),
                    1 => data.Slice(data.Length / 4, data.Length / 4),
                    2 => data.Slice((data.Length / 4) * 2, data.Length / 4),
                    3 => data.Slice((data.Length / 4) * 3, data.Length / 4),
                    _ => throw new InvalidDataException()
                };
            }

            void UpdateRateAndCoerce(bool forceRateUpdate)
            {
                // This logic should match the hardware/libtslitex logic with regards to coercing the rate
                bool rateChanged = false;
                switch (currentHardwareConfig.Acquisition.Resolution)
                {
                    case AdcResolution.EightBit:
                        switch (BitOperations.PopCount(currentHardwareConfig.Acquisition.EnabledChannels))
                        {
                            case 2:
                                if (currentHardwareConfig.Acquisition.SampleRateHz > 500_000_000)
                                {
                                    currentHardwareConfig.Acquisition.SampleRateHz = 500_000_000;
                                    rateChanged = true;
                                }
                                break;
                            case 3:
                            case 4:
                                if (currentHardwareConfig.Acquisition.SampleRateHz > 250_000_000)
                                {
                                    currentHardwareConfig.Acquisition.SampleRateHz = 250_000_000;
                                    rateChanged = true;
                                }
                                break;
                        }
                        break;
                    case AdcResolution.TwelveBit:
                        switch (BitOperations.PopCount(currentHardwareConfig.Acquisition.EnabledChannels))
                        {
                            case 1:
                                if (currentHardwareConfig.Acquisition.SampleRateHz > 660_000_000)
                                {
                                    currentHardwareConfig.Acquisition.SampleRateHz = 660_000_000;
                                    rateChanged = true;
                                }
                                break;
                            case 2:
                                if (currentHardwareConfig.Acquisition.SampleRateHz > 330_000_000)
                                {
                                    currentHardwareConfig.Acquisition.SampleRateHz = 330_000_000;
                                    rateChanged = true;
                                }
                                break;
                            case 3:
                            case 4:
                                if (currentHardwareConfig.Acquisition.SampleRateHz > 165_000_000)
                                {
                                    currentHardwareConfig.Acquisition.SampleRateHz = 165_000_000;
                                    rateChanged = true;
                                }
                                break;
                        }
                        break;
                }
                if (rateChanged || forceRateUpdate)
                    thunderscope.SetRate(currentHardwareConfig.Acquisition.SampleRateHz);
            }

            void ResetAll()
            {
                logger.LogDebug("ResetAll");

                // Reset acquisition buffers
                acquisitionBuffer.Reset();

                // Reset capture buffers
                captureBuffer.Configure(BitOperations.PopCount(currentHardwareConfig.Acquisition.EnabledChannels), processingConfig.ChannelDataLength, processingConfig.ChannelDataType);

                ResetTrigger();
            }

            void ResetTrigger()
            {
                // Reset triggers
                if (processingConfig.TriggerChannel == TriggerChannel.None)
                {
                    logger.LogWarning($"Trigger channel set to None");
                    return;
                }
                var triggerChannel = currentHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);

                triggerI8 = processingConfig.TriggerType switch
                {
                    TriggerType.Edge => processingConfig.EdgeTriggerParameters.Direction switch
                    {
                        EdgeDirection.Rising => new RisingEdgeTriggerI8(processingConfig.EdgeTriggerParameters, triggerChannel.ActualVoltFullScale),
                        EdgeDirection.Falling => new FallingEdgeTriggerI8(processingConfig.EdgeTriggerParameters, triggerChannel.ActualVoltFullScale),
                        EdgeDirection.Any => new AnyEdgeTriggerI8(processingConfig.EdgeTriggerParameters, triggerChannel.ActualVoltFullScale),
                        _ => throw new NotImplementedException()
                    },
                    TriggerType.Burst => new BurstTriggerI8(processingConfig.BurstTriggerParameters),
                    _ => throw new NotImplementedException()
                };
                triggerI16 = processingConfig.TriggerType switch
                {
                    TriggerType.Edge => processingConfig.EdgeTriggerParameters.Direction switch
                    {
                        EdgeDirection.Rising => new RisingEdgeTriggerI16(processingConfig.EdgeTriggerParameters, currentHardwareConfig.Acquisition.Resolution, triggerChannel.ActualVoltFullScale),
                        EdgeDirection.Falling => throw new NotImplementedException(),
                        EdgeDirection.Any => throw new NotImplementedException(),
                        _ => throw new NotImplementedException()
                    },
                    TriggerType.Burst => throw new NotImplementedException(),
                    _ => throw new NotImplementedException()
                };

                // Set trigger horizontal parameters
                ulong femtosecondsPerSample = 1000000000000000 / currentHardwareConfig.Acquisition.SampleRateHz;
                long windowTriggerPosition = (long)(processingConfig.TriggerDelayFs / femtosecondsPerSample);
                long additionalHoldoff = (long)(processingConfig.TriggerHoldoffFs / femtosecondsPerSample);
                triggerI8.SetHorizontal(processingConfig.ChannelDataLength, windowTriggerPosition, additionalHoldoff);
                triggerI16.SetHorizontal(processingConfig.ChannelDataLength, windowTriggerPosition, additionalHoldoff);
            }

            byte CalculateChannelMask(byte existingMask, int channelIndex, bool enabled)
            {
                byte newMask = existingMask;
                if (enabled)
                {
                    newMask |= (byte)(1 << channelIndex);
                }
                else
                {
                    newMask &= (byte)~(1 << channelIndex);
                }
                return newMask;
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
            logger.LogDebug("Stopped");
            //NativeMemory.AlignedFree(processingBufferP);
            //NativeMemory.AlignedFree(processingBufferI16P);
            //NativeMemory.AlignedFree(processingBufferI16P_2);
        }
    }
}
