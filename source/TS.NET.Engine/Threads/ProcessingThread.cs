using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TS.NET.Engine;

public class ProcessingThread : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly ThunderscopeHardwareConfig hardwareConfig;
    private readonly BlockingPool<DataDto> preProcessingPool;
    private readonly BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl;
    private readonly BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl;
    private readonly BlockingChannelWriter<INotificationDto>? uiNotifications;
    private readonly CaptureCircularBuffer captureBuffer;

    private CancellationTokenSource? cancelTokenSource;
    private Task? taskLoop;

    public ProcessingThread(
        ILogger logger,
        ThunderscopeSettings settings,
        ThunderscopeHardwareConfig hardwareConfig,
        BlockingPool<DataDto> preProcessingPool,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        BlockingChannelWriter<INotificationDto>? uiNotifications,
        CaptureCircularBuffer captureBuffer)
    {
        this.logger = logger;
        this.settings = settings;
        this.hardwareConfig = hardwareConfig;
        this.preProcessingPool = preProcessingPool;
        this.hardwareControl = hardwareControl;
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
            cachedHardwareConfig: hardwareConfig,
            preProcessingPool: preProcessingPool,
            hardwareControl: hardwareControl,
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
        ThunderscopeHardwareConfig cachedHardwareConfig,
        BlockingPool<DataDto> preProcessingPool,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
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

            // Set some sensible defaults
            ushort initialChannelCount = cachedHardwareConfig.AdcChannelMode switch
            {
                AdcChannelMode.Quad => 4,
                AdcChannelMode.Dual => 2,
                AdcChannelMode.Single => 1,
                _ => throw new NotImplementedException()
            };
            var processingConfig = new ThunderscopeProcessingConfig
            {
                ChannelCount = initialChannelCount,
                ChannelDataLength = 1000,
                ChannelDataType = ThunderscopeDataType.I8,
                Mode = Mode.Auto,
                TriggerChannel = TriggerChannel.Channel1,
                TriggerType = TriggerType.Edge,
                TriggerDelayFs = (ulong)(1e15 / (1e9 / initialChannelCount) * 500),   // Set the trigger delay to the middle of the capture
                TriggerHoldoffFs = 0,
                TriggerInterpolation = true,
                AutoTimeoutMs = 1000,
                EdgeTriggerParameters = new EdgeTriggerParameters() { Level = 0, Hysteresis = 5, Direction = EdgeDirection.Rising },
                BurstTriggerParameters = new BurstTriggerParameters() { WindowHighLevel = 64, WindowLowLevel = -64, MinimumInRangePeriod = 450000 },
                BoxcarAveraging = BoxcarAveraging.None
            };
            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
            uiNotifications?.TryWrite(new ProcessingStopDto());

            // Periodic debug display variables
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            ulong totalDequeueCount = 0;
            ulong cachedTotalDequeueCount = 0;

            Stopwatch periodicUpdateTimer = Stopwatch.StartNew();

            var sampleBuffers = new AcquisitionCircularBuffer[4];
            for (int i = 0; i < 4; i++)
            {
                sampleBuffers[i] = new AcquisitionCircularBuffer(settings.MaxCaptureLength, ThunderscopeSettings.SegmentLengthBytes, ThunderscopeDataType.I16);
            }
            captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);

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

            ITriggerI8 triggerI8 = new RisingEdgeTriggerI8(processingConfig.EdgeTriggerParameters);
            ITriggerI16 triggerI16 = new RisingEdgeTriggerI16(processingConfig.EdgeTriggerParameters);
            EdgeTriggerResults edgeTriggerResults = new EdgeTriggerResults()
            {
                ArmIndices = new int[ThunderscopeSettings.SegmentLengthBytes / 1000],         // 1000 samples is the minimum window width
                TriggerIndices = new int[ThunderscopeSettings.SegmentLengthBytes / 1000],     // 1000 samples is the minimum window width
                CaptureEndIndices = new int[ThunderscopeSettings.SegmentLengthBytes / 1000]   // 1000 samples is the minimum window width
            };
            bool runMode = false;
            bool forceTriggerLatch = false;
            bool singleTriggerLatch = false;    // "Latch" because it will reset state back to false. When reset, runTrigger will be set to false.
            Mode modeAfterForce = processingConfig.Mode;

            // Variables for Auto triggering
            Stopwatch autoTimeoutTimer = Stopwatch.StartNew();
            int streamSampleCounter = 0;

            logger.LogInformation("Started");
            startSemaphore.Release();

            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                while (processingControl.Request.Reader.TryRead(out var request))
                {
                    switch (request)
                    {
                        case ProcessingRunDto processingRunDto:
                            captureBuffer.Reset();
                            if (processingConfig.Mode == Mode.Single)
                                singleTriggerLatch = true;
                            StartHardware();
                            uiNotifications?.TryWrite(processingRunDto);
                            logger.LogDebug($"{nameof(ProcessingRunDto)}");
                            break;
                        case ProcessingStopDto processingStopDto:
                            StopHardware();
                            uiNotifications?.TryWrite(processingStopDto);
                            logger.LogDebug($"{nameof(ProcessingStopDto)}");
                            break;
                        case ProcessingForceDto processingForceDto:
                            if (runMode)        // FORCE is ignored if not in runMode.
                            {
                                modeAfterForce = processingConfig.Mode;
                                forceTriggerLatch = true;
                                uiNotifications?.TryWrite(processingForceDto);
                                logger.LogDebug($"{nameof(ProcessingForceDto)}");
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
                            // Convert the internal trigger level back to volts
                            var triggerChannelFrontend = cachedHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);
                            double levelVolts = (processingConfig.EdgeTriggerParameters.Level / 127.0) * (triggerChannelFrontend.ActualVoltFullScale / 2);
                            processingControl.Response.Writer.Write(new ProcessingGetEdgeTriggerLevelResponse(levelVolts));
                            logger.LogDebug($"{nameof(ProcessingGetEdgeTriggerLevelRequest)}");
                            break;
                        case ProcessingGetEdgeTriggerDirectionRequest processingGetEdgeTriggerDirectionRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetEdgeTriggerDirectionResponse(processingConfig.EdgeTriggerParameters.Direction));
                            logger.LogDebug($"{nameof(ProcessingGetEdgeTriggerDirectionRequest)}");
                            break;
                        case ProcessingSetModeDto processingSetModeDto:
                            singleTriggerLatch = false;
                            captureBuffer.Reset();              // To do: consider if resetting capture buffer is right in all mode change scenarios
                            switch (processingSetModeDto.Mode)
                            {
                                case Mode.Normal:                // NORMAL/STREAM/AUTO use RUN/STOP on user demand
                                case Mode.Stream:
                                    processingConfig.Mode = processingSetModeDto.Mode;
                                    break;
                                case Mode.Auto:
                                    autoTimeoutTimer.Restart();
                                    processingConfig.Mode = processingSetModeDto.Mode;
                                    break;
                                case Mode.Single:                // SINGLE forces runMode.
                                    if (runMode != true)
                                    {
                                        StartHardware();
                                    }
                                    singleTriggerLatch = true;
                                    processingConfig.Mode = processingSetModeDto.Mode;
                                    break;
                            }
                            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                            logger.LogDebug($"{nameof(ProcessingSetModeDto)} (mode: {processingConfig.Mode})");
                            break;
                        case ProcessingSetTriggerSourceDto processingSetTriggerSourceDto:
                            processingConfig.TriggerChannel = processingSetTriggerSourceDto.Channel;
                            captureBuffer.Reset();
                            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                            logger.LogDebug($"{nameof(ProcessingSetTriggerSourceDto)} (channel: {processingConfig.TriggerChannel})");
                            break;
                        case ProcessingSetTriggerTypeDto processingSetTriggerTypeDto:
                            if (processingConfig.TriggerType != processingSetTriggerTypeDto.Type)
                            {
                                processingConfig.TriggerType = processingSetTriggerTypeDto.Type;

                                SwitchTrigger();
                                UpdateTriggerHorizontal(cachedHardwareConfig);

                                captureBuffer.Reset();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerTypeDto)} (type: {processingConfig.TriggerType})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerTypeDto)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerDelayDto processingSetTriggerDelayDto:
                            if (processingConfig.TriggerDelayFs != processingSetTriggerDelayDto.Femtoseconds)
                            {
                                processingConfig.TriggerDelayFs = processingSetTriggerDelayDto.Femtoseconds;
                                UpdateTriggerHorizontal(cachedHardwareConfig);
                                captureBuffer.Reset();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelayDto)} (femtoseconds: {processingConfig.TriggerDelayFs})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelayDto)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerHoldoffDto processingSetTriggerHoldoffDto:
                            if (processingConfig.TriggerHoldoffFs != processingSetTriggerHoldoffDto.Femtoseconds)
                            {
                                processingConfig.TriggerHoldoffFs = processingSetTriggerHoldoffDto.Femtoseconds;
                                UpdateTriggerHorizontal(cachedHardwareConfig);
                                captureBuffer.Reset();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetTriggerHoldoffDto)} (femtoseconds: {processingConfig.TriggerHoldoffFs})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelayDto)} (no change)");
                            }
                            break;
                        case ProcessingSetEdgeTriggerLevelDto processingSetTriggerLevelDto:
                            var requestedTriggerLevel = processingSetTriggerLevelDto.LevelVolts;
                            // Convert the voltage to Int8

                            if (processingConfig.TriggerChannel == TriggerChannel.None)
                            {
                                logger.LogWarning($"Could not set trigger level {requestedTriggerLevel}");
                                break;
                            }
                            var triggerChannel = cachedHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);

                            if ((requestedTriggerLevel > triggerChannel.ActualVoltFullScale / 2) || (requestedTriggerLevel < -triggerChannel.ActualVoltFullScale / 2))
                            {
                                logger.LogWarning($"Could not set trigger level {requestedTriggerLevel}");
                                break;
                            }

                            sbyte triggerLevel = (sbyte)((requestedTriggerLevel / (triggerChannel.ActualVoltFullScale / 2)) * 127f);
                            if (triggerLevel != processingConfig.EdgeTriggerParameters.Level)
                            {
                                processingConfig.EdgeTriggerParameters.Level = triggerLevel;
                                UpdateTriggerParameters();

                                captureBuffer.Reset();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerLevelDto)} (level: {triggerLevel}, hysteresis: {processingConfig.EdgeTriggerParameters.Hysteresis})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerLevelDto)} (no change)");
                            }
                            break;
                        case ProcessingSetTriggerInterpolationDto processingSetTriggerInterpolation:
                            processingConfig.TriggerInterpolation = processingSetTriggerInterpolation.Enabled;
                            uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                            logger.LogDebug($"{nameof(ProcessingSetTriggerInterpolationDto)} (enabled: {processingSetTriggerInterpolation.Enabled})");
                            break;
                        case ProcessingSetEdgeTriggerDirectionDto processingSetEdgeTriggerDirection:
                            if (processingConfig.EdgeTriggerParameters.Direction != processingSetEdgeTriggerDirection.Edge)
                            {
                                processingConfig.EdgeTriggerParameters.Direction = processingSetEdgeTriggerDirection.Edge;

                                if (processingConfig.TriggerType == TriggerType.Edge)
                                {
                                    // Normally a trigger parameter update would call "UpdateTriggerParameters()" but this is a special case where "SwitchTrigger()" is needed as the edge changed.
                                    SwitchTrigger();
                                    UpdateTriggerHorizontal(cachedHardwareConfig);
                                }
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerDirectionDto)} (direction: {processingSetEdgeTriggerDirection.Edge})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerDirectionDto)} (no change)");
                            }
                            break;
                        case ProcessingSetDepthDto processingSetDepthDto:
                            if (processingConfig.ChannelDataLength != processingSetDepthDto.Samples)
                            {
                                processingConfig.ChannelDataLength = processingSetDepthDto.Samples;
                                captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);
                                UpdateTriggerHorizontal(cachedHardwareConfig);
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetDepthDto)} ({processingConfig.ChannelDataLength})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetDepthDto)} (no change)");
                            }
                            break;
                        case ProcessingSetBoxcarFilter processingSetBoxcarFilter:
                            //processingConfig.BoxcarAveraging = processingSetBoxcarFilter.Averages;
                            //processingConfig.ChannelDataType = ThunderscopeDataType.I16;
                            //uiNotifications?.TryWrite(processingSetBoxcarFilter);
                            //logger.LogDebug($"{nameof(ProcessingSetBoxcarFilter)} (averages: {processingSetBoxcarFilter.Averages})");
                            break;
                        default:
                            logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                            break;
                    }
                }

                if (preProcessingPool.Source.Reader.TryRead(out var dataDto, 10, cancelToken))
                {
                    totalDequeueCount++;
                    if (dataDto == null)
                        break;

                    if (dataDto.HardwareConfig.SampleRateHz != cachedHardwareConfig.SampleRateHz)
                    {
                        UpdateTriggerHorizontal(dataDto.HardwareConfig);
                        captureBuffer.Reset();
                        logger.LogTrace("Hardware sample rate change ({sampleRateHz})", dataDto.HardwareConfig.SampleRateHz);
                    }

                    if (dataDto.HardwareConfig.EnabledChannelsCount() != cachedHardwareConfig.EnabledChannelsCount())
                    {
                        processingConfig.ChannelCount = dataDto.HardwareConfig.EnabledChannelsCount();
                        captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);
                        logger.LogTrace("Hardware enabled channel change ({channelCount})", processingConfig.ChannelCount);
                    }

                    if (dataDto.MemoryType != processingConfig.ChannelDataType)
                    {
                        processingConfig.ChannelDataType = dataDto.MemoryType;
                        captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);
                        ResetSampleBuffers();
                        logger.LogTrace("Memory type change ({channelDataType})", processingConfig.ChannelDataType);
                    }

                    cachedHardwareConfig = dataDto.HardwareConfig;

                    switch (cachedHardwareConfig.AdcChannelMode)
                    {
                        case AdcChannelMode.Single:
                            // Write to circular sample buffer
                            switch (processingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    sampleBuffers[0].Write<sbyte>(dataDto.Memory.DataSpanI8);
                                    streamSampleCounter += dataDto.Memory.DataSpanI8.Length;
                                    break;
                                case ThunderscopeDataType.I16:
                                    sampleBuffers[0].Write<short>(dataDto.Memory.DataSpanI16);
                                    streamSampleCounter += dataDto.Memory.DataSpanI16.Length;
                                    break;
                            }
                            break;
                        case AdcChannelMode.Dual:
                            // Write to circular sample buffer
                            switch (processingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    {
                                        var span = dataDto.Memory.DataSpanI8;
                                        sampleBuffers[0].Write<sbyte>(Span2Ch(0, span));
                                        sampleBuffers[1].Write<sbyte>(Span2Ch(1, span));
                                        streamSampleCounter += span.Length / 2;
                                        break;
                                    }
                                case ThunderscopeDataType.I16:
                                    {
                                        var span = dataDto.Memory.DataSpanI16;
                                        sampleBuffers[0].Write<short>(Span2Ch(0, span));
                                        sampleBuffers[1].Write<short>(Span2Ch(1, span));
                                        streamSampleCounter += span.Length / 2;
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
                                        var span = dataDto.Memory.DataSpanI8;
                                        sampleBuffers[0].Write<sbyte>(Span4Ch(0, span));
                                        sampleBuffers[1].Write<sbyte>(Span4Ch(1, span));
                                        sampleBuffers[2].Write<sbyte>(Span4Ch(2, span));
                                        sampleBuffers[3].Write<sbyte>(Span4Ch(3, span));
                                        streamSampleCounter += span.Length / 4;
                                        break;
                                    }
                                case ThunderscopeDataType.I16:
                                    {
                                        var span = dataDto.Memory.DataSpanI16;
                                        sampleBuffers[0].Write<short>(Span4Ch(0, span));
                                        sampleBuffers[1].Write<short>(Span4Ch(1, span));
                                        sampleBuffers[2].Write<short>(Span4Ch(2, span));
                                        sampleBuffers[3].Write<short>(Span4Ch(3, span));
                                        streamSampleCounter += span.Length / 4;
                                        break;
                                    }
                            }
                            break;
                    }

                    if (runMode)
                    {
                        switch (processingConfig.Mode)
                        {
                            case Mode.Normal:
                            case Mode.Single:
                            case Mode.Auto:
                                if (cachedHardwareConfig.IsTriggerChannelAnEnabledChannel(processingConfig.TriggerChannel))
                                {
                                    Span<sbyte> triggerChannelBufferI8;
                                    Span<short> triggerChannelBufferI16;
                                    int triggerChannelCaptureIndex;
                                    switch (cachedHardwareConfig.AdcChannelMode)
                                    {
                                        case AdcChannelMode.Single:
                                            triggerChannelCaptureIndex = 0;
                                            triggerChannelBufferI8 = dataDto.Memory.DataSpanI8;
                                            triggerChannelBufferI16 = dataDto.Memory.DataSpanI16;
                                            break;
                                        case AdcChannelMode.Dual:
                                            triggerChannelCaptureIndex = cachedHardwareConfig.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                            triggerChannelBufferI8 = triggerChannelCaptureIndex switch
                                            {
                                                0 => Span2Ch(0, dataDto.Memory.DataSpanI8),
                                                1 => Span2Ch(1, dataDto.Memory.DataSpanI8),
                                                _ => throw new NotImplementedException()
                                            };
                                            triggerChannelBufferI16 = triggerChannelCaptureIndex switch
                                            {
                                                0 => Span2Ch(0, dataDto.Memory.DataSpanI16),
                                                1 => Span2Ch(1, dataDto.Memory.DataSpanI16),
                                                _ => throw new NotImplementedException()
                                            };
                                            break;
                                        case AdcChannelMode.Quad:
                                            triggerChannelCaptureIndex = cachedHardwareConfig.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                            triggerChannelBufferI8 = triggerChannelCaptureIndex switch
                                            {
                                                0 => Span4Ch(0, dataDto.Memory.DataSpanI8),
                                                1 => Span4Ch(1, dataDto.Memory.DataSpanI8),
                                                2 => Span4Ch(2, dataDto.Memory.DataSpanI8),
                                                3 => Span4Ch(3, dataDto.Memory.DataSpanI8),
                                                _ => throw new NotImplementedException()
                                            };
                                            triggerChannelBufferI16 = triggerChannelCaptureIndex switch
                                            {
                                                0 => Span4Ch(0, dataDto.Memory.DataSpanI16),
                                                1 => Span4Ch(1, dataDto.Memory.DataSpanI16),
                                                2 => Span4Ch(2, dataDto.Memory.DataSpanI16),
                                                3 => Span4Ch(3, dataDto.Memory.DataSpanI16),
                                                _ => throw new NotImplementedException()
                                            };
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }

                                    switch (processingConfig.ChannelDataType)
                                    {
                                        case ThunderscopeDataType.I8:
                                            triggerI8.Process(input: triggerChannelBufferI8, ref edgeTriggerResults);
                                            break;
                                        case ThunderscopeDataType.I16:
                                            triggerI16.Process(input: triggerChannelBufferI16, ref edgeTriggerResults);
                                            break;
                                    }

                                    if (edgeTriggerResults.CaptureEndCount > 0)
                                    {
                                        for (int i = 0; i < edgeTriggerResults.CaptureEndCount; i++)
                                        {
                                            int offset = triggerChannelBufferI8.Length - edgeTriggerResults.CaptureEndIndices[i];
                                            Capture(triggered: true, triggerChannelCaptureIndex, offset);

                                            if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                            {
                                                singleTriggerLatch = false;
                                                StopHardware();
                                                break;
                                            }
                                        }
                                        streamSampleCounter = 0;
                                        autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened
                                    }
                                }
                                if (forceTriggerLatch)
                                {
                                    Capture(triggered: false, triggerChannelCaptureIndex: 0, offset: 0);
                                    forceTriggerLatch = false;
                                    streamSampleCounter = 0;
                                    autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened
                                }
                                if (processingConfig.Mode == Mode.Auto && autoTimeoutTimer.ElapsedMilliseconds > processingConfig.AutoTimeoutMs)
                                {
                                    StreamCapture();
                                }
                                break;
                            case Mode.Stream:
                                StreamCapture();
                                break;
                        }
                    }

                    // Finished with the memory, return it
                    preProcessingPool.Return.Writer.Write(dataDto);

                    var elapsedTime = periodicUpdateTimer.Elapsed.TotalSeconds;
                    if (elapsedTime >= 10)
                    {
                        var dequeueCount = totalDequeueCount - cachedTotalDequeueCount;

                        var intervalCaptureTotal = captureBuffer.IntervalCaptureTotal;
                        var intervalCaptureDrops = captureBuffer.IntervalCaptureDrops;
                        var intervalCaptureReads = captureBuffer.IntervalCaptureReads;

                        logger.LogDebug($"[Capture stats] total/s: {intervalCaptureTotal / elapsedTime:F2}, drops/s: {intervalCaptureDrops / elapsedTime:F2}, UI reads/s: {intervalCaptureReads / elapsedTime:F2}");
                        logger.LogDebug($"[Capture buffer] capacity: {captureBuffer.MaxCaptureCount}, current: {captureBuffer.CurrentCaptureCount}, channel count: {captureBuffer.ChannelCount}, total: {captureBuffer.CaptureTotal}, drops: {captureBuffer.CaptureDrops}, reads: {captureBuffer.CaptureReads}");
                        periodicUpdateTimer.Restart();

                        cachedTotalDequeueCount = totalDequeueCount;
                        captureBuffer.ResetIntervalStats();
                    }
                }
            }

            // Locally scoped methods for deduplication
            void StartHardware()
            {
                runMode = true;
                hardwareControl.Request.Writer.Write(new HardwareStartRequest());
            }

            void StopHardware()
            {
                runMode = false;
                forceTriggerLatch = false;
                hardwareControl.Request.Writer.Write(new HardwareStopRequest());
                // Wait for response before clearing pool
                if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                {
                    switch (response)
                    {
                        case HardwareStopResponse hardwareStopResponse:
                            int i = 0;
                            while (preProcessingPool.Source.Reader.TryRead(out var inputDataDto, 10, cancelToken))
                            {
                                i++;
                                if (inputDataDto != null)
                                    preProcessingPool.Return.Writer.Write(inputDataDto);
                            }
                            break;
                        default:
                            throw new UnreachableException($"Invalid response to {nameof(HardwareStopRequest)}");
                    }
                }
                SwitchTrigger();    // Reset trigger so that if the trigger has been left in an ARM state, it doesn't prematurely trigger
            }

            void SwitchTrigger()
            {
                triggerI8 = processingConfig.TriggerType switch
                {
                    TriggerType.Edge => processingConfig.EdgeTriggerParameters.Direction switch
                    {
                        EdgeDirection.Rising => new RisingEdgeTriggerI8(processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Falling => new FallingEdgeTriggerI8(processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Any => new AnyEdgeTriggerI8(processingConfig.EdgeTriggerParameters),
                        _ => throw new NotImplementedException()
                    },
                    TriggerType.Burst => new BurstTriggerI8(processingConfig.BurstTriggerParameters),
                    _ => throw new NotImplementedException()
                };
                triggerI16 = processingConfig.TriggerType switch
                {
                    TriggerType.Edge => processingConfig.EdgeTriggerParameters.Direction switch
                    {
                        EdgeDirection.Rising => new RisingEdgeTriggerI16(processingConfig.EdgeTriggerParameters),
                        EdgeDirection.Falling => throw new NotImplementedException(),
                        EdgeDirection.Any => throw new NotImplementedException(),
                        _ => throw new NotImplementedException()
                    },
                    TriggerType.Burst => throw new NotImplementedException(),
                    _ => throw new NotImplementedException()
                };
            }

            void UpdateTriggerHorizontal(ThunderscopeHardwareConfig hardwareConfig)
            {
                ulong femtosecondsPerSample = 1000000000000000 / hardwareConfig.SampleRateHz;
                long windowTriggerPosition = (long)(processingConfig.TriggerDelayFs / femtosecondsPerSample);
                long additionalHoldoff = (long)(processingConfig.TriggerHoldoffFs / femtosecondsPerSample);
                logger.LogTrace($"{additionalHoldoff}");
                triggerI8.SetHorizontal(processingConfig.ChannelDataLength, windowTriggerPosition, additionalHoldoff);
                triggerI16.SetHorizontal(processingConfig.ChannelDataLength, windowTriggerPosition, additionalHoldoff);
            }

            void UpdateTriggerParameters()
            {
                switch (triggerI8)
                {
                    case RisingEdgeTriggerI8 risingEdgeTriggerI8:
                        risingEdgeTriggerI8.SetParameters(processingConfig.EdgeTriggerParameters);
                        break;
                    case FallingEdgeTriggerI8 fallingEdgeTriggerI8:
                        fallingEdgeTriggerI8.SetParameters(processingConfig.EdgeTriggerParameters);
                        break;
                    case AnyEdgeTriggerI8 anyEdgeTriggerI8:
                        anyEdgeTriggerI8.SetParameters(processingConfig.EdgeTriggerParameters);
                        break;
                    case BurstTriggerI8 burstTriggerI8:
                        burstTriggerI8.SetParameters(processingConfig.BurstTriggerParameters);
                        break;
                    default:
                        throw new NotImplementedException();
                }
                switch (triggerI16)
                {
                    case RisingEdgeTriggerI16 risingEdgeTriggerI16:
                        risingEdgeTriggerI16.SetParameters(processingConfig.EdgeTriggerParameters);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            void Capture(bool triggered, int triggerChannelCaptureIndex, int offset)
            {
                if (captureBuffer.TryStartWrite())
                {
                    int channelCount = cachedHardwareConfig.EnabledChannelsCount();
                    for (int b = 0; b < channelCount; b++)
                    {
                        switch (processingConfig.ChannelDataType)
                        {
                            case ThunderscopeDataType.I8:
                                {
                                    var buffer = captureBuffer.GetChannelWriteBuffer<sbyte>(b);
                                    sampleBuffers[b].Read(buffer, offset);
                                    break;
                                }
                            case ThunderscopeDataType.I16:
                                {
                                    var buffer = captureBuffer.GetChannelWriteBuffer<short>(b);
                                    sampleBuffers[b].Read(buffer, offset);
                                    break;
                                }
                        }
                    }
                    var captureMetadata = new CaptureMetadata
                    {
                        Triggered = triggered,
                        TriggerChannelCaptureIndex = triggerChannelCaptureIndex,
                        HardwareConfig = cachedHardwareConfig,
                        ProcessingConfig = processingConfig
                    };
                    captureBuffer.FinishWrite(captureMetadata);
                }
            }

            void StreamCapture()
            {
                int channelLength = processingConfig.ChannelDataLength;
                while (streamSampleCounter > channelLength)
                {
                    streamSampleCounter -= channelLength;
                    Capture(triggered: false, triggerChannelCaptureIndex: 0, offset: 0);
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

            void ResetSampleBuffers()
            {
                foreach (var sampleBuffer in sampleBuffers)
                    sampleBuffer.Reset();
                streamSampleCounter = 0;
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
