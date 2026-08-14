using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics;

namespace TS.NET.Engine;

public class ProcessingThread : IThread
{
    private readonly ILogger logger;
    private readonly CancellationTokenSource appCancellationTokenSource;
    private readonly ThunderscopeSettings settings;
    private readonly IThunderscope thunderscope;
    private readonly BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl;
    private readonly BlockingChannelWriter<INotificationDto>? uiNotifications;
    private readonly CaptureBufferManager captureBufferManager;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskLoop;

    public ProcessingThread(
        ILogger logger,
        CancellationTokenSource appCancellationTokenSource,
        ThunderscopeSettings settings,
        IThunderscope thunderscope,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        BlockingChannelWriter<INotificationDto>? uiNotifications,
        CaptureBufferManager captureBufferManager)
    {
        this.logger = logger;
        this.appCancellationTokenSource = appCancellationTokenSource;
        this.settings = settings;
        this.thunderscope = thunderscope;
        this.processingControl = processingControl;
        this.uiNotifications = uiNotifications;
        this.captureBufferManager = captureBufferManager;
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        cancelTokenSource = new CancellationTokenSource();
        taskLoop = Task.Factory.StartNew(() => Loop(
            logger: logger,
            appCancellationTokenSource: appCancellationTokenSource,
            settings: settings,
            thunderscope: thunderscope,
            processingControl: processingControl,
            uiNotifications: uiNotifications,
            captureBufferManager: captureBufferManager,
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
        CancellationTokenSource appCancellationTokenSource,
        ThunderscopeSettings settings,
        IThunderscope thunderscope,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        BlockingChannelWriter<INotificationDto>? uiNotifications,
        CaptureBufferManager captureBufferManager,
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
                ChannelDataLength = 1_000_000,
                ChannelDataType = initialChannelDataType,
                Mode = Mode.Normal,     // Temporary, change back to AUTO when NotImplementedException fixed
                TriggerChannel = TriggerChannel.Channel1,
                TriggerType = TriggerType.Edge,
                TriggerDelayFs = (ulong)(1e15 / (1e9 / initialChannelCount) * 500),   // Set the trigger delay to the middle of the capture
                TriggerHoldoffFs = 0,
                TriggerInterpolation = true,
                AutoTimeoutMs = 1000,
                EdgeTriggerParameters = new EdgeTriggerParameters() { LevelV = 0, Direction = EdgeDirection.Rising, HysteresisPercent = 5 },
                WindowTriggerParameters = new WindowTriggerParameters() { UpperLevelV = 1, LowerLevelV = -1, Direction = WindowDirection.Enter },
                BurstTriggerParameters = new BurstTriggerParameters() { LevelV = 0, Direction = BurstEdgeDirection.Rising, HysteresisPercent = 5, QuietUpperLevelV = 1, QuietLowerLevelV = -1, QuietTimeFs = 1000000000000L },
                BoxcarAveraging = BoxcarAveraging.None
            };
            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
            uiNotifications?.TryWrite(new ProcessingStop());
            var currentHardwareConfig = initialHardwareConfig;

            // Periodic debug display variables
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            long totalReadChunks = 0;
            long totalReadBytes = 0;
            long totalReadSamplesPerChannel = 0;

            long periodicReadChunks = 0;
            long periodicReadBytes = 0;
            long periodicReadSamplesPerChannel = 0;

            long periodicCaptureSamplesPerChannel = 0;

            Stopwatch periodicUpdateTimer = Stopwatch.StartNew();

            var acquisitionBuffer = new AcquisitionCircularBuffer(settings.MaxCaptureLength, ThunderscopeSettings.SegmentLengthBytes, ThunderscopeDataType.I16);

            captureBufferManager.Configure(initialChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);

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

            ITriggerI8? triggerI8 = null;
            ITriggerI16? triggerI16 = null;
            IEventTrigger? eventTrigger = null;
            ResetTrigger();

            var edgeTriggerResults = new EdgeTriggerResults()
            {
                ArmIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000],         // 1000 samples is the minimum window width
                TriggerIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000],     // 1000 samples is the minimum window width
                CaptureEndIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000]   // 1000 samples is the minimum window width
            };
            var eventTriggerResults = new EventTriggerResults()
            {
                CaptureEndIndices = new ulong[ThunderscopeSettings.SegmentLengthBytes / 1000],         // 1000 samples is the minimum window width
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
            bool startWhenAllProcessingControlRequestsProcessed = false;

            logger.LogDebug("Started");
            startSemaphore.Release();

            //Start();

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
                                ResetBuffers();
                                ResetTrigger();
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
                                ResetBuffers();
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(HardwareSetResolution)} ({hardwareSetResolution.Resolution})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(HardwareSetResolution)} (no change)");
                            }
                            break;
                        case HardwareSetChannelEnabled hardwareSetChannelEnabled:
                            var enabledChannels = CalculateChannelMask(currentHardwareConfig.Acquisition.EnabledChannels, hardwareSetChannelEnabled.ChannelIndex, hardwareSetChannelEnabled.Enabled);
                            if (currentHardwareConfig.Acquisition.EnabledChannels != enabledChannels)
                            {
                                UpdateRateAndCoerce(forceRateUpdate: false);
                                thunderscope.SetChannelEnable(hardwareSetChannelEnabled.ChannelIndex, hardwareSetChannelEnabled.Enabled);
                                currentHardwareConfig = thunderscope.GetConfiguration();
                                ResetBuffers();
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(HardwareSetChannelEnabled)} ({hardwareSetChannelEnabled.ChannelIndex} {hardwareSetChannelEnabled.Enabled})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(HardwareSetChannelEnabled)} (no change)");
                            }
                            break;

                        case HardwareSetRefClockMode hardwareSetRefClockMode:
                            {
                                currentHardwareConfig.RefClockMode = hardwareSetRefClockMode.Mode;
                                thunderscope.SetRefClockMode(hardwareSetRefClockMode.Mode);
                                logger.LogDebug($"{nameof(HardwareSetRefClockMode)} ({hardwareSetRefClockMode.Mode})");
                                break;
                            }
                        case HardwareSetRefClockFrequency hardwareSetRefClockFrequency:
                            {
                                currentHardwareConfig.RefClockFrequencyHz = hardwareSetRefClockFrequency.FrequencyHz;
                                thunderscope.SetRefClockFrequency(hardwareSetRefClockFrequency.FrequencyHz);
                                logger.LogDebug($"{nameof(HardwareSetRefClockFrequency)} ({hardwareSetRefClockFrequency.FrequencyHz})");
                                break;
                            }

                        case HardwareSetChannelFrontendRequest hardwareSetChannelFrontendRequest:
                            {
                                var channelIndex = hardwareSetChannelFrontendRequest.ChannelIndex;
                                var channelFrontend = thunderscope.GetChannelFrontend(channelIndex);
                                switch (request)
                                {
                                    case HardwareSetVoltOffset hardwareSetVoltOffset:
                                        channelFrontend.RequestedVoltOffset = hardwareSetVoltOffset.VoltOffset;
                                        break;
                                    case HardwareSetVoltFullScale hardwareSetVoltFullScale:
                                        channelFrontend.RequestedVoltFullScale = hardwareSetVoltFullScale.VoltFullScale;
                                        break;
                                    case HardwareSetBandwidth hardwareSetBandwidth:
                                        logger.LogDebug($"{nameof(HardwareSetBandwidth)} (channel: {channelIndex}, bandwidth: {hardwareSetBandwidth.Bandwidth})");
                                        channelFrontend.Bandwidth = hardwareSetBandwidth.Bandwidth;
                                        break;
                                    case HardwareSetCoupling hardwareSetCoupling:
                                        logger.LogDebug($"{nameof(HardwareSetCoupling)} (channel: {channelIndex}, coupling: {hardwareSetCoupling.Coupling})");
                                        channelFrontend.Coupling = hardwareSetCoupling.Coupling;
                                        break;
                                    case HardwareSetTermination hardwareSetTermination:
                                        logger.LogDebug($"{nameof(HardwareSetTermination)} (channel: {channelIndex}, termination: {hardwareSetTermination.Termination})");
                                        channelFrontend.RequestedTermination = hardwareSetTermination.Termination;
                                        break;
                                    default:
                                        logger.LogWarning($"Unknown {nameof(HardwareSetChannelFrontendRequest)}: {request}");
                                        break;
                                }
                                var reset = runMode;
                                if (reset)
                                {
                                    Stop();     // 1ms
                                    startWhenAllProcessingControlRequestsProcessed = true;
                                }
                                thunderscope.SetChannelFrontend(channelIndex, channelFrontend);
                                switch (request)
                                {
                                    case HardwareSetVoltOffset hardwareSetVoltOffset:
                                        logger.LogDebug($"{nameof(HardwareSetVoltOffset)} (channel: {channelIndex}, requested: {hardwareSetVoltOffset.VoltOffset}, actual: {currentHardwareConfig.Frontend[channelIndex].ActualVoltOffset:F4}, min: {currentHardwareConfig.Frontend[channelIndex].MinVoltOffset:F4}, max: {currentHardwareConfig.Frontend[channelIndex].MaxVoltOffset:F4})");
                                        break;
                                    case HardwareSetVoltFullScale hardwareSetVoltFullScale:
                                        logger.LogDebug($"{nameof(HardwareSetVoltFullScale)} (channel: {channelIndex}, requested: {hardwareSetVoltFullScale.VoltFullScale}, actual: {currentHardwareConfig.Frontend[channelIndex].ActualVoltFullScale:F4})");
                                        break;

                                }
                                break;
                            }

                        case HardwareSetChannelManualControl hardwareSetChannelManualControl:
                            {
                                if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                                {
                                    liteXThunderscope.SetChannelManualControl(hardwareSetChannelManualControl.ChannelIndex, hardwareSetChannelManualControl.Channel);
                                }
                                logger.LogDebug($"{nameof(HardwareSetChannelManualControl)} (channel: {hardwareSetChannelManualControl.ChannelIndex})");
                                break;
                            }
                        case HardwareSetAdcBranchGainsManualControl hardwareSetAdcBranchGainsManualControl:
                            {
                                var ts = (Driver.Libtslitex.Thunderscope)thunderscope;
                                ts.SetAdcBranchGainManualControl(hardwareSetAdcBranchGainsManualControl.Gains);
                                logger.LogDebug($"{nameof(HardwareSetAdcBranchGainsManualControl)}");
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
                                var channelIndex = hardwareGetChannelFrontendRequest.ChannelIndex;
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
                        case HardwareGetTemperatureRequest hardwareGetTemperatureRequest:
                            {
                                float temp = 25.0f;
                                if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                                {
                                    var status = liteXThunderscope.GetStatus();
                                    temp = (float)status.FpgaTemp;
                                }
                                processingControl.Response.Writer.Write(new HardwareGetTemperatureResponse(temp));
                                logger.LogDebug($"{nameof(HardwareGetTemperatureRequest)}");
                                break;
                            }

                        case ProcessingRun processingRun:
                            if (processingConfig.Mode == Mode.Single)
                                singleTriggerLatch = true;
                            startWhenAllProcessingControlRequestsProcessed = true;
                            uiNotifications?.TryWrite(processingRun);
                            logger.LogDebug($"{nameof(ProcessingRun)}");
                            break;
                        case ProcessingStop processingStop:
                            Stop();
                            startWhenAllProcessingControlRequestsProcessed = false;
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
                            ResetBuffers();
                            ResetTrigger();
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
                                        startWhenAllProcessingControlRequestsProcessed = true;
                                    }
                                    singleTriggerLatch = true;
                                    processingConfig.Mode = processingSetMode.Mode;
                                    break;
                            }
                            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                            logger.LogDebug($"{nameof(ProcessingSetMode)} (mode: {processingConfig.Mode})");
                            break;
                        case ProcessingSetDepth processingSetDepth:
                            if (processingConfig.ChannelDataLength != processingSetDepth.Samples)
                            {
                                processingConfig.ChannelDataLength = processingSetDepth.Samples;
                                ResetBuffers();
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetDepth)} ({processingConfig.ChannelDataLength})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetDepth)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerSource processingSetTriggerSource:
                            if (processingConfig.TriggerChannel != processingSetTriggerSource.Channel)
                            {
                                // If coming out of external trigger mode, disable external trigger input
                                if (processingConfig.TriggerChannel == TriggerChannel.External)
                                {
                                    currentHardwareConfig.ExtSyncMode = ThunderscopeExtSyncMode.Disabled;
                                    thunderscope.SetExtSyncMode(currentHardwareConfig.ExtSyncMode);
                                }

                                processingConfig.TriggerChannel = processingSetTriggerSource.Channel;

                                // If going into external trigger mode, enable external trigger input
                                if (processingConfig.TriggerChannel == TriggerChannel.External)
                                {
                                    currentHardwareConfig.ExtSyncMode = ThunderscopeExtSyncMode.Input;
                                    thunderscope.SetExtSyncMode(currentHardwareConfig.ExtSyncMode);
                                }

                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSource)} (channel: {processingConfig.TriggerChannel})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSource)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerType processingSetTriggerType:
                            if (processingConfig.TriggerType != processingSetTriggerType.Type)
                            {
                                processingConfig.TriggerType = processingSetTriggerType.Type;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerType)} (type: {processingConfig.TriggerType})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerType)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerDelay processingSetTriggerDelay:
                            if (processingConfig.TriggerDelayFs != processingSetTriggerDelay.Femtoseconds)
                            {
                                processingConfig.TriggerDelayFs = processingSetTriggerDelay.Femtoseconds;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelay)} (femtoseconds: {processingConfig.TriggerDelayFs})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelay)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerHoldoff processingSetTriggerHoldoff:
                            if (processingConfig.TriggerHoldoffFs != processingSetTriggerHoldoff.Femtoseconds)
                            {
                                processingConfig.TriggerHoldoffFs = processingSetTriggerHoldoff.Femtoseconds;
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
                        case ProcessingSetEdgeTriggerLevel processingSetEdgeTriggerLevel:
                            var requestedTriggerLevel = processingSetEdgeTriggerLevel.LevelVolts;
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
                        case ProcessingSetEdgeTriggerHysteresis processingSetEdgeTriggerHysteresis:
                            if (processingConfig.EdgeTriggerParameters.HysteresisPercent != processingSetEdgeTriggerHysteresis.Percent)
                            {
                                processingConfig.EdgeTriggerParameters.HysteresisPercent = processingSetEdgeTriggerHysteresis.Percent;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerHysteresis)} (percent: {processingConfig.EdgeTriggerParameters.HysteresisPercent})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerHysteresis)} (no change)");
                            }
                            break;
                        case ProcessingSetWindowTriggerUpperLevel processingSetWindowTriggerUpperLevel:
                            if (processingConfig.WindowTriggerParameters.UpperLevelV != processingSetWindowTriggerUpperLevel.LevelVolts)
                            {
                                processingConfig.WindowTriggerParameters.UpperLevelV = processingSetWindowTriggerUpperLevel.LevelVolts;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetWindowTriggerUpperLevel)} (level: {processingConfig.WindowTriggerParameters.UpperLevelV})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetWindowTriggerUpperLevel)} (no change)");
                            }
                            break;
                        case ProcessingSetWindowTriggerLowerLevel processingSetWindowTriggerLowerLevel:
                            if (processingConfig.WindowTriggerParameters.LowerLevelV != processingSetWindowTriggerLowerLevel.LevelVolts)
                            {
                                processingConfig.WindowTriggerParameters.LowerLevelV = processingSetWindowTriggerLowerLevel.LevelVolts;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetWindowTriggerLowerLevel)} (level: {processingConfig.WindowTriggerParameters.LowerLevelV})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetWindowTriggerLowerLevel)} (no change)");
                            }
                            break;
                        case ProcessingSetWindowTriggerDirection processingSetWindowTriggerDirection:
                            if (processingConfig.WindowTriggerParameters.Direction != processingSetWindowTriggerDirection.Direction)
                            {
                                processingConfig.WindowTriggerParameters.Direction = processingSetWindowTriggerDirection.Direction;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetWindowTriggerDirection)} (direction: {processingConfig.WindowTriggerParameters.Direction})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetWindowTriggerDirection)} (no change)");
                            }
                            break;
                        case ProcessingSetBurstTriggerLevel processingSetBurstTriggerLevel:
                            if (processingConfig.BurstTriggerParameters.LevelV != processingSetBurstTriggerLevel.LevelVolts)
                            {
                                processingConfig.BurstTriggerParameters.LevelV = processingSetBurstTriggerLevel.LevelVolts;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerLevel)} (level: {processingConfig.BurstTriggerParameters.LevelV})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerLevel)} (no change)");
                            }
                            break;
                        case ProcessingSetBurstTriggerDirection processingSetBurstTriggerDirection:
                            if (processingConfig.BurstTriggerParameters.Direction != processingSetBurstTriggerDirection.Edge)
                            {
                                processingConfig.BurstTriggerParameters.Direction = processingSetBurstTriggerDirection.Edge;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerDirection)} (direction: {processingConfig.BurstTriggerParameters.Direction})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerDirection)} (no change)");
                            }
                            break;
                        case ProcessingSetBurstTriggerHysteresis processingSetBurstTriggerHysteresis:
                            if (processingConfig.BurstTriggerParameters.HysteresisPercent != processingSetBurstTriggerHysteresis.Percent)
                            {
                                processingConfig.BurstTriggerParameters.HysteresisPercent = processingSetBurstTriggerHysteresis.Percent;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerHysteresis)} (percent: {processingConfig.BurstTriggerParameters.HysteresisPercent})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerHysteresis)} (no change)");
                            }
                            break;
                        case ProcessingSetBurstTriggerQuietUpperLevel processingSetBurstTriggerQuietUpperLevel:
                            if (processingConfig.BurstTriggerParameters.QuietUpperLevelV != processingSetBurstTriggerQuietUpperLevel.LevelVolts)
                            {
                                processingConfig.BurstTriggerParameters.QuietUpperLevelV = processingSetBurstTriggerQuietUpperLevel.LevelVolts;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerQuietUpperLevel)} (level: {processingConfig.BurstTriggerParameters.QuietUpperLevelV})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerQuietUpperLevel)} (no change)");
                            }
                            break;
                        case ProcessingSetBurstTriggerQuietLowerLevel processingSetBurstTriggerQuietLowerLevel:
                            if (processingConfig.BurstTriggerParameters.QuietLowerLevelV != processingSetBurstTriggerQuietLowerLevel.LevelVolts)
                            {
                                processingConfig.BurstTriggerParameters.QuietLowerLevelV = processingSetBurstTriggerQuietLowerLevel.LevelVolts;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerQuietLowerLevel)} (level: {processingConfig.BurstTriggerParameters.QuietLowerLevelV})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerQuietLowerLevel)} (no change)");
                            }
                            break;
                        case ProcessingSetBurstTriggerQuietTime processingSetBurstTriggerQuietTime:
                            if (processingConfig.BurstTriggerParameters.QuietTimeFs != processingSetBurstTriggerQuietTime.Femtoseconds)
                            {
                                processingConfig.BurstTriggerParameters.QuietTimeFs = processingSetBurstTriggerQuietTime.Femtoseconds;
                                ResetTrigger();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerQuietTime)} (femtoseconds: {processingConfig.BurstTriggerParameters.QuietTimeFs})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetBurstTriggerQuietTime)} (no change)");
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
                        case ProcessingGetEdgeTriggerHysteresisRequest processingGetEdgeTriggerHysteresisRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetEdgeTriggerHysteresisResponse(processingConfig.EdgeTriggerParameters.HysteresisPercent));
                            logger.LogDebug($"{nameof(ProcessingGetEdgeTriggerHysteresisRequest)}");
                            break;
                        case ProcessingGetWindowTriggerUpperLevelRequest processingGetWindowTriggerUpperLevelRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetWindowTriggerUpperLevelResponse(processingConfig.WindowTriggerParameters.UpperLevelV));
                            logger.LogDebug($"{nameof(ProcessingGetWindowTriggerUpperLevelRequest)}");
                            break;
                        case ProcessingGetWindowTriggerLowerLevelRequest processingGetWindowTriggerLowerLevelRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetWindowTriggerLowerLevelResponse(processingConfig.WindowTriggerParameters.LowerLevelV));
                            logger.LogDebug($"{nameof(ProcessingGetWindowTriggerLowerLevelRequest)}");
                            break;
                        case ProcessingGetWindowTriggerDirectionRequest processingGetWindowTriggerDirectionRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetWindowTriggerDirectionResponse(processingConfig.WindowTriggerParameters.Direction));
                            logger.LogDebug($"{nameof(ProcessingGetWindowTriggerDirectionRequest)}");
                            break;
                        case ProcessingGetBurstTriggerLevelRequest processingGetBurstTriggerLevelRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetBurstTriggerLevelResponse(processingConfig.BurstTriggerParameters.LevelV));
                            logger.LogDebug($"{nameof(ProcessingGetBurstTriggerLevelRequest)}");
                            break;
                        case ProcessingGetBurstTriggerDirectionRequest processingGetBurstTriggerDirectionRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetBurstTriggerDirectionResponse(processingConfig.BurstTriggerParameters.Direction));
                            logger.LogDebug($"{nameof(ProcessingGetBurstTriggerDirectionRequest)}");
                            break;
                        case ProcessingGetBurstTriggerHysteresisRequest processingGetBurstTriggerHysteresisRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetBurstTriggerHysteresisResponse(processingConfig.BurstTriggerParameters.HysteresisPercent));
                            logger.LogDebug($"{nameof(ProcessingGetBurstTriggerHysteresisRequest)}");
                            break;
                        case ProcessingGetBurstTriggerQuietUpperLevelRequest processingGetBurstTriggerQuietUpperLevelRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetBurstTriggerQuietUpperLevelResponse(processingConfig.BurstTriggerParameters.QuietUpperLevelV));
                            logger.LogDebug($"{nameof(ProcessingGetBurstTriggerQuietUpperLevelRequest)}");
                            break;
                        case ProcessingGetBurstTriggerQuietLowerLevelRequest processingGetBurstTriggerQuietLowerLevelRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetBurstTriggerQuietLowerLevelResponse(processingConfig.BurstTriggerParameters.QuietLowerLevelV));
                            logger.LogDebug($"{nameof(ProcessingGetBurstTriggerQuietLowerLevelRequest)}");
                            break;
                        case ProcessingGetBurstTriggerQuietTimeRequest processingGetBurstTriggerQuietTimeRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetBurstTriggerQuietTimeResponse(processingConfig.BurstTriggerParameters.QuietTimeFs));
                            logger.LogDebug($"{nameof(ProcessingGetBurstTriggerQuietTimeRequest)}");
                            break;

                        case ProcessingGetRatesRequest processingGetRatesRequest:
                            {
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

                if (startWhenAllProcessingControlRequestsProcessed)
                {
                    currentHardwareConfig = thunderscope.GetConfiguration();        // Required for ResetTrigger() to set the correct trigger level
                    ResetBuffers();         // 30ms
                    ResetTrigger();         // 0.1ms
                    runMode = true;
                    thunderscope.Start();   // 3ms         
                    startWhenAllProcessingControlRequestsProcessed = false;
                }

                if (thunderscope.Running())
                {
                    if (thunderscope.TryRead(preShuffleMemory.DataSpanU8, out var sampleStartIndex, out var sampleLengthPerChannel))
                    {
                        totalReadChunks++;
                        totalReadBytes += preShuffleMemory.LengthBytes;
                        totalReadSamplesPerChannel += sampleLengthPerChannel;
                        periodicReadChunks++;
                        periodicReadBytes += preShuffleMemory.LengthBytes;
                        periodicReadSamplesPerChannel += sampleLengthPerChannel;

                        // Shuffle
                        // Possible improvement: postShuffleMemory could come from the acquisitionBuffer,
                        // and in the single channel case, be a straight memcopy to keep memory bandwidth
                        // roughly the same between single/dual/quad.
                        switch (currentHardwareConfig.Acquisition.AdcChannelMode)
                        {
                            case AdcChannelMode.Single:
                                (preShuffleMemory, postShuffleMemory) = (postShuffleMemory, preShuffleMemory); // No shuffle, swap memory
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
                                            logger.LogWarning("Unoptimised ShuffleI16.TwoChannels, missing Ssse3 & Arm64 paths");
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
                                            logger.LogWarning("Unoptimised ShuffleI16.FourChannels, missing Ssse3 & Arm64 paths");
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
                                        if (acquisitionBuffer.SamplesInBufferPerChannel >= processingConfig.ChannelDataLength)
                                        {
                                            switch (processingConfig.ChannelDataType)
                                            {
                                                case ThunderscopeDataType.I8:
                                                    Capture<sbyte>(triggered: false, triggerChannelCaptureIndex: 0, captureEndIndex: sampleStartIndex + (ulong)sampleLengthPerChannel);
                                                    break;
                                                case ThunderscopeDataType.I16:
                                                    Capture<short>(triggered: false, triggerChannelCaptureIndex: 0, captureEndIndex: sampleStartIndex + (ulong)sampleLengthPerChannel);
                                                    break;
                                            }
                                            forceTriggerLatch = false;
                                            autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened

                                            if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                            {
                                                singleTriggerLatch = false;
                                                Stop();
                                                break;
                                            }
                                        }
                                    }
                                    if (processingConfig.TriggerChannel == TriggerChannel.External)
                                    {
                                        while (thunderscope.TryGetEvent(out var thunderscopeEvent, out var eventSampleIndex))
                                        {
                                            //logger.LogDebug($"Event. eventSampleIndex: {eventSampleIndex}, sampleStartIndex: {sampleStartIndex}");
                                            eventTrigger?.EnqueueEvent(eventSampleIndex);
                                        }

                                        eventTrigger?.Process(sampleLengthPerChannel, sampleStartIndex, acquisitionBuffer.SamplesInBufferPerChannel, ref eventTriggerResults);

                                        if (eventTriggerResults.CaptureEndCount > 0)
                                        {
                                            for (int i = 0; i < eventTriggerResults.CaptureEndCount; i++)
                                            {
                                                //logger.LogDebug($"Capture {eventTriggerResults.CaptureEndIndices[i]}");
                                                switch (processingConfig.ChannelDataType)
                                                {
                                                    case ThunderscopeDataType.I8:
                                                        Capture<sbyte>(triggered: false, triggerChannelCaptureIndex: 0, eventTriggerResults.CaptureEndIndices[i]);
                                                        break;
                                                    case ThunderscopeDataType.I16:
                                                        Capture<short>(triggered: false, triggerChannelCaptureIndex: 0, eventTriggerResults.CaptureEndIndices[i]);
                                                        break;
                                                }
                                                if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                                {
                                                    singleTriggerLatch = false;
                                                    Stop();
                                                    break;
                                                }
                                            }
                                            autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened
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
                                                triggerI8?.Process(input: triggerChannelBufferI8, sampleStartIndex, ref edgeTriggerResults);
                                                break;
                                            case ThunderscopeDataType.I16:
                                                triggerI16?.Process(input: triggerChannelBufferI16, sampleStartIndex, ref edgeTriggerResults);
                                                break;
                                        }

                                        if (edgeTriggerResults.CaptureEndCount > 0)
                                        {
                                            for (int i = 0; i < edgeTriggerResults.CaptureEndCount; i++)
                                            {
                                                switch (processingConfig.ChannelDataType)
                                                {
                                                    case ThunderscopeDataType.I8:
                                                        Capture<sbyte>(triggered: true, triggerChannelCaptureIndex, edgeTriggerResults.CaptureEndIndices[i]);
                                                        break;
                                                    case ThunderscopeDataType.I16:
                                                        Capture<short>(triggered: true, triggerChannelCaptureIndex, edgeTriggerResults.CaptureEndIndices[i]);
                                                        break;
                                                }
                                                if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                                {
                                                    singleTriggerLatch = false;
                                                    Stop();
                                                    break;
                                                }
                                            }
                                            autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened
                                        }
                                    }

                                    if (processingConfig.Mode == Mode.Auto && autoTimeoutTimer.ElapsedMilliseconds > processingConfig.AutoTimeoutMs)
                                    {
                                        switch (processingConfig.ChannelDataType)
                                        {
                                            case ThunderscopeDataType.I8:
                                                StreamCapture<sbyte>();
                                                break;
                                            case ThunderscopeDataType.I16:
                                                StreamCapture<short>();
                                                break;
                                        }

                                    }
                                    break;
                                case Mode.Stream:
                                    switch (processingConfig.ChannelDataType)
                                    {
                                        case ThunderscopeDataType.I8:
                                            StreamCapture<sbyte>();
                                            break;
                                        case ThunderscopeDataType.I16:
                                            StreamCapture<short>();
                                            break;
                                    }
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }

                // Debug information
                var elapsedTime = periodicUpdateTimer.Elapsed.TotalSeconds;
                if (elapsedTime >= 10)
                {
                    var oneSecondReadBytes = periodicReadBytes / periodicUpdateTimer.Elapsed.TotalSeconds;
                    logger.LogDebug($"[Stream] MB/sec: {(oneSecondReadBytes / 1000 / 1000):F3}, MiB/sec: {(oneSecondReadBytes / 1024 / 1024):F3}");

                    if (thunderscope is Driver.Libtslitex.Thunderscope liteXThunderscope)
                    {
                        var status = liteXThunderscope.GetStatus();
                        logger.LogDebug($"[LiteX] lost buffers: {status.AdcSamplesLost}, temp: {status.FpgaTemp:F2}, VCC int: {status.VccInt:F3}, VCC aux: {status.VccAux:F3}, VCC BRAM: {status.VccBram:F3}, ADC Sync: {status.AdcFrameSync}, ref clock in: {status.RefClockInValid}");
                    }

                    var intervalCaptureWrites = captureBufferManager.IntervalCaptureWrites;
                    var intervalCaptureDrops = captureBufferManager.IntervalCaptureDrops;
                    var intervalCaptureReads = captureBufferManager.IntervalCaptureReads;

                    var sampleReadPercent = 0.0;
                    if (periodicCaptureSamplesPerChannel > 0)
                    {
                        sampleReadPercent = ((double)periodicCaptureSamplesPerChannel / (double)periodicReadSamplesPerChannel) * 100.0;
                        if (sampleReadPercent > 100.0)
                            sampleReadPercent = 100.0;
                    }

                    var captureReadPercent = 0.0;
                    if (intervalCaptureWrites > 0)
                    {
                        captureReadPercent = ((double)intervalCaptureReads / (double)intervalCaptureWrites) * 100.0;
                        if (captureReadPercent > 100.0)
                            captureReadPercent = 100.0;
                    }

                    logger.LogDebug($"[Capture stats] writes/s: {intervalCaptureWrites / elapsedTime:F2}, reads/s: {intervalCaptureReads / elapsedTime:F2}, drops/s: {intervalCaptureDrops / elapsedTime:F2}");
                    logger.LogDebug($"[Capture stats #2] {sampleReadPercent:F1}% samples captured, {captureReadPercent:F0}% captures read by DataServer");
                    logger.LogDebug($"[Capture buffer] capacity: {captureBufferManager.MaxCaptureCount}, current: {captureBufferManager.CurrentCaptureCount}, writes: {captureBufferManager.CaptureWrites}, reads: {captureBufferManager.CaptureReads}, drops: {captureBufferManager.CaptureDrops}");
                    periodicUpdateTimer.Restart();

                    periodicReadChunks = 0;
                    periodicReadBytes = 0;
                    periodicReadSamplesPerChannel = 0;
                    periodicCaptureSamplesPerChannel = 0;
                    captureBufferManager.ResetIntervalStats();
                }
            }

            // Locally scoped methods for deduplication
            void Capture<T>(bool triggered, int triggerChannelCaptureIndex, ulong captureEndIndex) where T : unmanaged
            {
                // Capture buffer should only have insufficient length when running in AUTO. NORMAL/SINGLE will throw an exception later in this method.
                if (processingConfig.Mode == Mode.Auto && acquisitionBuffer.SamplesInBufferPerChannel < processingConfig.ChannelDataLength)
                {
                    logger.LogDebug("Capture skipped due to insufficient samples in buffer during AUTO");
                    return;
                }

                if (captureBufferManager.TryStartWrite(out var buffer))
                {
                    int channelCount = currentHardwareConfig.Acquisition.EnabledChannelsCount();
                    switch (channelCount)
                    {
                        case 1:
                            {
                                var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                acquisitionBuffer.Read1ChannelWithEndIndex(buffer1, captureEndIndex);
                                periodicCaptureSamplesPerChannel += buffer1.Length;
                            }
                            break;
                        case 2:
                            {
                                var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                var buffer2 = buffer!.GetChannelWriteBuffer<T>(1);
                                acquisitionBuffer.Read2ChannelWithEndIndex(buffer1, buffer2, captureEndIndex);
                                periodicCaptureSamplesPerChannel += buffer1.Length;
                            }
                            break;
                        case 3:
                            {
                                var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                var buffer2 = buffer!.GetChannelWriteBuffer<T>(1);
                                var buffer3 = buffer!.GetChannelWriteBuffer<T>(2);
                                acquisitionBuffer.Read3ChannelWithEndIndex(buffer1, buffer2, buffer3, captureEndIndex);
                                periodicCaptureSamplesPerChannel += buffer1.Length;
                            }
                            break;
                        case 4:
                            {
                                var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                var buffer2 = buffer!.GetChannelWriteBuffer<T>(1);
                                var buffer3 = buffer!.GetChannelWriteBuffer<T>(2);
                                var buffer4 = buffer!.GetChannelWriteBuffer<T>(3);
                                acquisitionBuffer.Read4ChannelWithEndIndex(buffer1, buffer2, buffer3, buffer4, captureEndIndex);
                                periodicCaptureSamplesPerChannel += buffer1.Length;
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    var captureMetadata = new CaptureMetadata
                    {
                        Triggered = triggered,
                        TriggerChannelCaptureIndex = triggerChannelCaptureIndex,
                        HardwareConfig = currentHardwareConfig,
                        ProcessingConfig = processingConfig
                    };
                    captureBufferManager.FinishWrite(captureMetadata);
                }
            }

            void StreamCapture<T>() where T : unmanaged
            {
                int channelLength = processingConfig.ChannelDataLength;
                while (acquisitionBuffer.SamplesInBufferPerChannel > channelLength)
                {
                    if (captureBufferManager.TryStartWrite(out var buffer))
                    {
                        int channelCount = currentHardwareConfig.Acquisition.EnabledChannelsCount();
                        switch (channelCount)
                        {
                            case 1:
                                {
                                    var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                    acquisitionBuffer.Read1ChannelFromStart(buffer1);
                                    periodicCaptureSamplesPerChannel += buffer1.Length;
                                }
                                break;
                            case 2:
                                {
                                    var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                    var buffer2 = buffer!.GetChannelWriteBuffer<T>(1);
                                    acquisitionBuffer.Read2ChannelFromStart(buffer1, buffer2);
                                    periodicCaptureSamplesPerChannel += buffer1.Length;
                                }
                                break;
                            case 3:
                                {
                                    var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                    var buffer2 = buffer!.GetChannelWriteBuffer<T>(1);
                                    var buffer3 = buffer!.GetChannelWriteBuffer<T>(2);
                                    acquisitionBuffer.Read3ChannelFromStart(buffer1, buffer2, buffer3);
                                    periodicCaptureSamplesPerChannel += buffer1.Length;
                                }
                                break;
                            case 4:
                                {
                                    var buffer1 = buffer!.GetChannelWriteBuffer<T>(0);
                                    var buffer2 = buffer!.GetChannelWriteBuffer<T>(1);
                                    var buffer3 = buffer!.GetChannelWriteBuffer<T>(2);
                                    var buffer4 = buffer!.GetChannelWriteBuffer<T>(3);
                                    acquisitionBuffer.Read4ChannelFromStart(buffer1, buffer2, buffer3, buffer4);
                                    periodicCaptureSamplesPerChannel += buffer1.Length;
                                }
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        var captureMetadata = new CaptureMetadata
                        {
                            Triggered = false,
                            TriggerChannelCaptureIndex = -1,
                            HardwareConfig = currentHardwareConfig,
                            ProcessingConfig = processingConfig
                        };
                        captureBufferManager.FinishWrite(captureMetadata);
                    }
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

            void ResetBuffers()
            {
                logger.LogDebug("ResetBuffers");

                // Reset acquisition buffers
                acquisitionBuffer.Reset();

                // Reset capture buffers
                captureBufferManager.Configure(BitOperations.PopCount(currentHardwareConfig.Acquisition.EnabledChannels), processingConfig.ChannelDataLength, processingConfig.ChannelDataType);
            }

            void ResetTrigger()
            {
                logger.LogDebug("ResetTrigger");

                ulong femtosecondsPerSample = 1000000000000000 / currentHardwareConfig.Acquisition.SampleRateHz;
                long windowTriggerPosition = (long)(processingConfig.TriggerDelayFs / femtosecondsPerSample);
                long additionalHoldoff = (long)(processingConfig.TriggerHoldoffFs / femtosecondsPerSample);

                if (processingConfig.TriggerChannel == TriggerChannel.External)
                {
                    eventTrigger = new EventTrigger();
                    eventTrigger.SetHorizontal(processingConfig.ChannelDataLength, windowTriggerPosition, additionalHoldoff);
                    return;
                }

                if (processingConfig.TriggerChannel == TriggerChannel.None)
                {
                    logger.LogWarning($"Trigger channel set to None");
                    return;
                }
                var triggerChannel = currentHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);
                var triggerChannelParameters = new TriggerChannelParameters()
                {
                    SampleRateHz = currentHardwareConfig.Acquisition.SampleRateHz,
                    TriggerChannelVpp = (float)triggerChannel.ActualVoltFullScale,
                    TriggerChannelOffsetV = (float)triggerChannel.ActualVoltOffset
                };

                triggerI8 = processingConfig.TriggerType switch
                {
                    TriggerType.Edge => processingConfig.EdgeTriggerParameters.Direction switch
                    {
                        EdgeDirection.Rising => new RisingEdgeTriggerI8(triggerChannelParameters, processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Falling => new FallingEdgeTriggerI8(triggerChannelParameters, processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Any => new AnyEdgeTriggerI8(triggerChannelParameters, processingConfig.EdgeTriggerParameters),
                        _ => throw new NotImplementedException()
                    },
                    //TriggerType.Window => new WindowTriggerI8(triggerChannelParameters, processingConfig.WindowTriggerParameters),    // Not ready until hysteresis is implemented
                    TriggerType.Burst => new BurstTriggerI8(triggerChannelParameters, processingConfig.BurstTriggerParameters),
                    _ => throw new NotImplementedException()
                };
                triggerI16 = processingConfig.TriggerType switch
                {
                    TriggerType.Edge => processingConfig.EdgeTriggerParameters.Direction switch
                    {
                        EdgeDirection.Rising => new RisingEdgeTriggerI16(triggerChannelParameters, processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Falling => new FallingEdgeTriggerI16(triggerChannelParameters, processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Any => new AnyEdgeTriggerI16(triggerChannelParameters, processingConfig.EdgeTriggerParameters),
                        _ => throw new NotImplementedException()
                    },
                    //TriggerType.Window => new WindowTriggerI16(triggerChannelParameters, processingConfig.WindowTriggerParameters),   // Not ready until hysteresis is implemented
                    TriggerType.Burst => new BurstTriggerI16(triggerChannelParameters, processingConfig.BurstTriggerParameters),
                    _ => throw new NotImplementedException()
                };

                // Set trigger horizontal parameters
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

            void Stop()
            {
                runMode = false;
                forceTriggerLatch = false;
                thunderscope.Stop();
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Stopping...");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error");
            appCancellationTokenSource.Cancel();
        }
        finally
        {
            logger.LogDebug("Stopped");
        }
    }
}
