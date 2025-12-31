using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Numerics;

namespace TS.NET.Engine;

public class ProcessingThread : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly ThunderscopeHardwareConfig hardwareConfig;
    private readonly BlockingPool<DataDto> inputPool;
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
        BlockingPool<DataDto> inputPool,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        BlockingChannelWriter<INotificationDto>? uiNotifications,
        CaptureCircularBuffer captureBuffer)
    {
        this.logger = logger;
        this.settings = settings;
        this.hardwareConfig = hardwareConfig;
        this.inputPool = inputPool;
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
            hardwareConfig: hardwareConfig,
            inputPool: inputPool,
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
        ThunderscopeHardwareConfig hardwareConfig,
        BlockingPool<DataDto> inputPool,
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
            ushort initialChannelCount = hardwareConfig.Acquisition.AdcChannelMode switch
            {
                AdcChannelMode.Quad => 4,
                AdcChannelMode.Dual => 2,
                AdcChannelMode.Single => 1,
                _ => throw new NotImplementedException()
            };
            var initialChannelDataType = hardwareConfig.Acquisition.Resolution switch
            {
                AdcResolution.EightBit => ThunderscopeDataType.I8,
                AdcResolution.TwelveBit => ThunderscopeDataType.I16,
                _ => throw new NotImplementedException()
            };
            var processingConfig = new ThunderscopeProcessingConfig
            {
                AdcResolution = hardwareConfig.Acquisition.Resolution,
                SampleRateHz = 1_000_000_000,
                EnabledChannels = hardwareConfig.Acquisition.EnabledChannels,
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
            var cachedHardwareConfig = hardwareConfig;

            // Periodic debug display variables
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            ulong totalDequeueCount = 0;
            ulong cachedTotalDequeueCount = 0;

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

            logger.LogInformation("Started");
            startSemaphore.Release();

            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                while (processingControl.Request.Reader.TryRead(out var request))
                {
                    switch (request)
                    {
                        case ProcessingRun processingRun:
                            ResetAll();
                            if (processingConfig.Mode == Mode.Single)
                                singleTriggerLatch = true;
                            StartHardware();
                            uiNotifications?.TryWrite(processingRun);
                            logger.LogDebug($"{nameof(ProcessingRun)}");
                            break;
                        case ProcessingStop processingStop:
                            StopHardware();
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
                        case ProcessingGetRatesRequest processingGetRatesRequest:
                            {
                                logger.LogDebug($"{nameof(ProcessingGetRatesRequest)}");
                                List<ulong> rates = [];
                                switch (settings.HardwareDriver.ToLower())
                                {
                                    case "litex":
                                    case "libtslitex":
                                        {
                                            switch (BitOperations.PopCount(processingConfig.EnabledChannels))
                                            {
                                                case 1:
                                                    if (processingConfig.AdcResolution == AdcResolution.EightBit)
                                                        rates.Add(1_000_000_000);
                                                    rates.Add(660_000_000);
                                                    rates.Add(500_000_000);
                                                    rates.Add(330_000_000);
                                                    rates.Add(250_000_000);
                                                    rates.Add(165_000_000);
                                                    rates.Add(100_000_000);
                                                    break;
                                                case 2:
                                                    if (processingConfig.AdcResolution == AdcResolution.EightBit)
                                                        rates.Add(500_000_000);
                                                    rates.Add(330_000_000);
                                                    rates.Add(250_000_000);
                                                    rates.Add(165_000_000);
                                                    rates.Add(100_000_000);
                                                    break;
                                                case 3:
                                                case 4:
                                                    if (processingConfig.AdcResolution == AdcResolution.EightBit)
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
                                        StartHardware();
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
                        case ProcessingSetRate processingSetRate:
                            if (processingConfig.SampleRateHz != processingSetRate.Rate)
                            {
                                processingConfig.SampleRateHz = processingSetRate.Rate;
                                UpdateRateAndCoerce(forceRateUpdate: true);
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetRate)} ({processingConfig.SampleRateHz})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetRate)} (no change)");
                            }
                            break;
                        case ProcessingSetResolution processingSetResolution:
                            if (processingConfig.AdcResolution != processingSetResolution.Resolution)
                            {
                                processingConfig.AdcResolution = processingSetResolution.Resolution;
                                processingConfig.ChannelDataType = processingSetResolution.Resolution switch
                                {
                                    AdcResolution.EightBit => ThunderscopeDataType.I8,
                                    AdcResolution.TwelveBit => ThunderscopeDataType.I16,
                                    _ => throw new NotImplementedException()
                                };

                                UpdateRateAndCoerce(forceRateUpdate: false);
                                hardwareControl.Request.Writer.Write(new HardwareSetResolution(processingSetResolution.Resolution));
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetResolution)} ({processingSetResolution.Resolution})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetResolution)} (no change)");
                            }
                            break;
                        case ProcessingSetEnabled processingSetEnabled:
                            var enabledChannels = CalculateChannelMask(processingConfig.EnabledChannels, processingSetEnabled.ChannelIndex, processingSetEnabled.Enabled);
                            if (processingConfig.EnabledChannels != enabledChannels)
                            {
                                processingConfig.EnabledChannels = enabledChannels;
                                UpdateRateAndCoerce(forceRateUpdate: false);
                                hardwareControl.Request.Writer.Write(new HardwareSetEnabled(processingSetEnabled.ChannelIndex, processingSetEnabled.Enabled));
                                ResetAll();
                                uiNotifications?.TryWrite(NotificationMapper.ToNotification(processingConfig));
                                logger.LogDebug($"{nameof(ProcessingSetEnabled)} ({processingSetEnabled.ChannelIndex} {processingSetEnabled.Enabled})");
                            }
                            else
                            {
                                logger.LogDebug($"{nameof(ProcessingSetEnabled)} (no change)");
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
                        case ProcessingGetRateRequest processingGetRateRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetRateResponse(processingConfig.SampleRateHz));
                            logger.LogDebug($"{nameof(ProcessingGetRateRequest)}");
                            break;
                        case ProcessingGetResolutionRequest processingGetResolutionRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetResolutionResponse(processingConfig.AdcResolution));
                            logger.LogDebug($"{nameof(ProcessingGetResolutionRequest)}");
                            break;
                        case ProcessingGetEnabledRequest processingGetEnabledRequest:
                            processingControl.Response.Writer.Write(new ProcessingGetEnabledResponse(processingConfig.EnabledChannels));
                            logger.LogDebug($"{nameof(ProcessingGetEnabledRequest)}");
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
                        default:
                            logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                            break;
                    }
                }

                if (inputPool.Source.Reader.TryRead(out var dataDto, 10, cancelToken))
                {
                    totalDequeueCount++;
                    if (dataDto == null)
                        break;

                    if (dataDto.HardwareConfig.Acquisition.SampleRateHz != processingConfig.SampleRateHz)
                    {
                        logger.LogWarning("Dropped buffer (mismatched rates)");
                        inputPool.Return.Writer.Write(dataDto);
                        continue;   // Drop the buffer
                    }

                    if (dataDto.HardwareConfig.Acquisition.Resolution != processingConfig.AdcResolution)
                    {
                        logger.LogWarning("Dropped buffer (mismatched resolution)");
                        inputPool.Return.Writer.Write(dataDto);
                        continue;   // Drop the buffer
                    }

                    if (dataDto.HardwareConfig.Acquisition.EnabledChannels != processingConfig.EnabledChannels)
                    {
                        logger.LogWarning("Dropped buffer (mismatched enabled channels)");
                        inputPool.Return.Writer.Write(dataDto);
                        continue;   // Drop the buffer
                    }

                    if (dataDto.MemoryType != processingConfig.ChannelDataType)
                    {
                        logger.LogWarning("Dropped buffer (mismatched channel data type)");
                        inputPool.Return.Writer.Write(dataDto);
                        continue;   // Drop the buffer
                    }

                    cachedHardwareConfig = dataDto.HardwareConfig;

                    switch (dataDto.HardwareConfig.Acquisition.AdcChannelMode)
                    {
                        case AdcChannelMode.Single:
                            // Write to circular sample buffer
                            switch (processingConfig.ChannelDataType)
                            {
                                case ThunderscopeDataType.I8:
                                    acquisitionBuffer.Write1Channel<sbyte>(dataDto.Memory.DataSpanI8, dataDto.SampleStartIndex);
                                    break;
                                case ThunderscopeDataType.I16:
                                    acquisitionBuffer.Write1Channel<short>(dataDto.Memory.DataSpanI16, dataDto.SampleStartIndex);
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
                                        acquisitionBuffer.Write2Channel<sbyte>(Span2Ch(0, span), Span2Ch(1, span), dataDto.SampleStartIndex);
                                        break;
                                    }
                                case ThunderscopeDataType.I16:
                                    {
                                        var span = dataDto.Memory.DataSpanI16;
                                        acquisitionBuffer.Write2Channel<short>(Span2Ch(0, span), Span2Ch(1, span), dataDto.SampleStartIndex);
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
                                        acquisitionBuffer.Write4Channel<sbyte>(Span4Ch(0, span), Span4Ch(1, span), Span4Ch(2, span), Span4Ch(3, span), dataDto.SampleStartIndex);
                                        break;
                                    }
                                case ThunderscopeDataType.I16:
                                    {
                                        var span = dataDto.Memory.DataSpanI16;
                                        acquisitionBuffer.Write4Channel<short>(Span4Ch(0, span), Span4Ch(1, span), Span4Ch(2, span), Span4Ch(3, span), dataDto.SampleStartIndex);
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
                                        Capture(triggered: false, triggerChannelCaptureIndex: 0, captureEndIndex: dataDto.SampleStartIndex + (ulong)dataDto.SampleLength);
                                        forceTriggerLatch = false;
                                        autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened

                                        if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                        {
                                            singleTriggerLatch = false;
                                            StopHardware();
                                            break;
                                        }
                                    }
                                }
                                else if (dataDto.HardwareConfig.Acquisition.IsTriggerChannelAnEnabledChannel(processingConfig.TriggerChannel))
                                {
                                    Span<sbyte> triggerChannelBufferI8;
                                    Span<short> triggerChannelBufferI16;
                                    int triggerChannelCaptureIndex;
                                    switch (dataDto.HardwareConfig.Acquisition.AdcChannelMode)
                                    {
                                        case AdcChannelMode.Single:
                                            triggerChannelCaptureIndex = 0;
                                            triggerChannelBufferI8 = dataDto.Memory.DataSpanI8;
                                            triggerChannelBufferI16 = dataDto.Memory.DataSpanI16;
                                            break;
                                        case AdcChannelMode.Dual:
                                            triggerChannelCaptureIndex = dataDto.HardwareConfig.Acquisition.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
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
                                            triggerChannelCaptureIndex = dataDto.HardwareConfig.Acquisition.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
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
                                            triggerI8.Process(input: triggerChannelBufferI8, dataDto.SampleStartIndex, ref edgeTriggerResults);
                                            break;
                                        case ThunderscopeDataType.I16:
                                            triggerI16.Process(input: triggerChannelBufferI16, dataDto.SampleStartIndex, ref edgeTriggerResults);
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
                                                StopHardware();
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

                    // Finished with the memory, return it
                    inputPool.Return.Writer.Write(dataDto);

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
                hardwareControl.Request.Writer.Write(new HardwareStart());
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
                            while (inputPool.Source.Reader.TryRead(out var inputDataDto, 10, cancelToken))
                            {
                                i++;
                                if (inputDataDto != null)
                                    inputPool.Return.Writer.Write(inputDataDto);
                            }
                            break;
                        default:
                            throw new UnreachableException($"Invalid response to {nameof(HardwareStopRequest)}");
                    }
                }
            }

            void Capture(bool triggered, int triggerChannelCaptureIndex, ulong captureEndIndex)
            {
                if (captureBuffer.TryStartWrite())
                {
                    int channelCount = cachedHardwareConfig.Acquisition.EnabledChannelsCount();
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
                        HardwareConfig = cachedHardwareConfig,
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
                bool rateChanged = false;
                switch (processingConfig.AdcResolution)
                {
                    case AdcResolution.EightBit:
                        switch (BitOperations.PopCount(processingConfig.EnabledChannels))
                        {
                            case 2:
                                if (processingConfig.SampleRateHz > 500_000_000)
                                {
                                    processingConfig.SampleRateHz = 500_000_000;
                                    rateChanged = true;
                                }
                                break;
                            case 3:
                            case 4:
                                if (processingConfig.SampleRateHz > 250_000_000)
                                {
                                    processingConfig.SampleRateHz = 250_000_000;
                                    rateChanged = true;
                                }
                                break;
                        }
                        break;
                    case AdcResolution.TwelveBit:
                        switch (BitOperations.PopCount(processingConfig.EnabledChannels))
                        {
                            case 1:
                                if (processingConfig.SampleRateHz > 660_000_000)
                                {
                                    processingConfig.SampleRateHz = 660_000_000;
                                    rateChanged = true;
                                }
                                break;
                            case 2:
                                if (processingConfig.SampleRateHz > 330_000_000)
                                {
                                    processingConfig.SampleRateHz = 330_000_000;
                                    rateChanged = true;
                                }
                                break;
                            case 3:
                            case 4:
                                if (processingConfig.SampleRateHz > 165_000_000)
                                {
                                    processingConfig.SampleRateHz = 165_000_000;
                                    rateChanged = true;
                                }
                                break;
                        }
                        break;
                }
                if (rateChanged || forceRateUpdate)
                    hardwareControl.Request.Writer.Write(new HardwareSetRate(processingConfig.SampleRateHz));
            }

            void ResetAll()
            {
                logger.LogDebug("ResetAll");

                // Reset acquisition buffers
                acquisitionBuffer.Reset();

                // Reset capture buffers
                captureBuffer.Configure(BitOperations.PopCount(processingConfig.EnabledChannels), processingConfig.ChannelDataLength, processingConfig.ChannelDataType);

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
                var triggerChannel = cachedHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);

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
                        EdgeDirection.Rising => new RisingEdgeTriggerI16(processingConfig.EdgeTriggerParameters, processingConfig.AdcResolution, triggerChannel.ActualVoltFullScale),
                        EdgeDirection.Falling => throw new NotImplementedException(),
                        EdgeDirection.Any => throw new NotImplementedException(),
                        _ => throw new NotImplementedException()
                    },
                    TriggerType.Burst => throw new NotImplementedException(),
                    _ => throw new NotImplementedException()
                };

                // Set trigger horizontal parameters
                ulong femtosecondsPerSample = 1000000000000000 / processingConfig.SampleRateHz;
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
