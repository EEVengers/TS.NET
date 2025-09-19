using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TS.NET.Engine
{
    public class ProcessingThread : IEngineTask
    {
        private readonly ILogger logger;
        private readonly ThunderscopeSettings settings;
        private readonly ThunderscopeHardwareConfig hardwareConfig;
        private readonly BlockingChannelReader<InputDataDto> incomingDataChannel;
        private readonly BlockingChannelWriter<ThunderscopeMemory> memoryReturnChannel;
        private readonly BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel;
        private readonly BlockingChannelReader<ProcessingRequestDto> processingRequestChannel;
        private readonly BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel;
        private readonly CaptureCircularBuffer captureBuffer;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public ProcessingThread(
            ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            ThunderscopeHardwareConfig hardwareConfig,
            BlockingChannelReader<InputDataDto> incomingDataChannel,
            BlockingChannelWriter<ThunderscopeMemory> memoryReturnChannel,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel,
            CaptureCircularBuffer captureBuffer)
        {
            logger = loggerFactory.CreateLogger(nameof(ProcessingThread));
            this.settings = settings;
            this.hardwareConfig = hardwareConfig;
            this.incomingDataChannel = incomingDataChannel;
            this.memoryReturnChannel = memoryReturnChannel;
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.hardwareResponseChannel = hardwareResponseChannel;
            this.processingRequestChannel = processingRequestChannel;
            this.processingResponseChannel = processingResponseChannel;
            this.captureBuffer = captureBuffer;
        }

        public void Start(SemaphoreSlim startSemaphore)
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(
                logger: logger,
                settings: settings,
                cachedHardwareConfig: hardwareConfig,
                incomingDataChannel: incomingDataChannel,
                memoryReturnChannel: memoryReturnChannel,
                hardwareRequestChannel: hardwareRequestChannel,
                hardwareResponseChannel: hardwareResponseChannel,
                processingRequestChannel: processingRequestChannel,
                processingResponseChannel: processingResponseChannel,
                startSemaphore: startSemaphore,
                captureBuffer: captureBuffer,
                cancelToken: cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        // The job of this task - pull data from scope driver/simulator, shuffle if 2/4 channels, horizontal sum, trigger, and produce window segments.
        private static unsafe void Loop(
            ILogger logger,
            ThunderscopeSettings settings,
            ThunderscopeHardwareConfig cachedHardwareConfig,
            BlockingChannelReader<InputDataDto> incomingDataChannel,
            BlockingChannelWriter<ThunderscopeMemory> memoryReturnChannel,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel,
            SemaphoreSlim startSemaphore,
            CaptureCircularBuffer captureBuffer,
            CancellationToken cancelToken)
        {
            const int processingBufferLength_1Ch = ThunderscopeMemory.Length;
            var processingBufferI8P = NativeMemory.AlignedAlloc(processingBufferLength_1Ch * sizeof(sbyte), 32);
            var processingBufferI16P = NativeMemory.AlignedAlloc(processingBufferLength_1Ch * sizeof(short), 32);

            try
            {
                Thread.CurrentThread.Name = "Processing";
                if (settings.ProcessingThreadProcessorAffinity > -1)
                {
                    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                    {
                        Thread.BeginThreadAffinity();
                        OsThread.SetThreadAffinity(settings.ProcessingThreadProcessorAffinity);
                        logger.LogDebug($"{nameof(ProcessingThread)} thread processor affinity set to {settings.ProcessingThreadProcessorAffinity}");
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

                bool liteX = settings.HardwareDriver.ToLower() == "litex";

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

                Span<sbyte> processingBufferI8 = new Span<sbyte>((sbyte*)processingBufferI8P, processingBufferLength_1Ch);
                Span<short> processingBufferI16 = new Span<short>((short*)processingBufferI16P, processingBufferLength_1Ch);
                // --2 channel buffers
                const int processingBufferLength_2Ch = processingBufferLength_1Ch / 2;
                Span<sbyte> processingBufferI8_2Ch_1 = processingBufferI8.Slice(0, processingBufferLength_2Ch);
                Span<sbyte> processingBufferI8_2Ch_2 = processingBufferI8.Slice(processingBufferLength_2Ch, processingBufferLength_2Ch);
                Span<short> processingBufferI16_2Ch_1 = processingBufferI16.Slice(0, processingBufferLength_2Ch);
                Span<short> processingBufferI16_2Ch_2 = processingBufferI16.Slice(processingBufferLength_2Ch, processingBufferLength_2Ch);
                // --4 channel buffers
                const int shuffleBufferLength_4Ch = processingBufferLength_1Ch / 4;
                Span<sbyte> processingBufferI8_4Ch_1 = processingBufferI8.Slice(0, shuffleBufferLength_4Ch);
                Span<sbyte> processingBufferI8_4Ch_2 = processingBufferI8.Slice(shuffleBufferLength_4Ch, shuffleBufferLength_4Ch);
                Span<sbyte> processingBufferI8_4Ch_3 = processingBufferI8.Slice(shuffleBufferLength_4Ch * 2, shuffleBufferLength_4Ch);
                Span<sbyte> processingBufferI8_4Ch_4 = processingBufferI8.Slice(shuffleBufferLength_4Ch * 3, shuffleBufferLength_4Ch);
                Span<short> processingBufferI16_4Ch_1 = processingBufferI16.Slice(0, shuffleBufferLength_4Ch);
                Span<short> processingBufferI16_4Ch_2 = processingBufferI16.Slice(shuffleBufferLength_4Ch, shuffleBufferLength_4Ch);
                Span<short> processingBufferI16_4Ch_3 = processingBufferI16.Slice(shuffleBufferLength_4Ch * 2, shuffleBufferLength_4Ch);
                Span<short> processingBufferI16_4Ch_4 = processingBufferI16.Slice(shuffleBufferLength_4Ch * 3, shuffleBufferLength_4Ch);

                // Periodic debug display variables
                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                ulong totalDequeueCount = 0;
                ulong cachedTotalDequeueCount = 0;

                Stopwatch periodicUpdateTimer = Stopwatch.StartNew();

                var sampleBuffers = new AcquisitionCircularBuffer[4];
                for (int i = 0; i < 4; i++)
                {
                    sampleBuffers[i] = new AcquisitionCircularBuffer(settings.MaxCaptureLength, ThunderscopeDataType.I16);
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
                    ArmIndices = new int[ThunderscopeMemory.Length / 1000],         // 1000 samples is the minimum window width
                    TriggerIndices = new int[ThunderscopeMemory.Length / 1000],     // 1000 samples is the minimum window width
                    CaptureEndIndices = new int[ThunderscopeMemory.Length / 1000]   // 1000 samples is the minimum window width
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

                MovingAverageFilterI16 movingAverageFilterI16 = new(10);

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    while (processingRequestChannel.TryRead(out var request))
                    {
                        switch (request)
                        {
                            case ProcessingRunDto processingRunDto:
                                captureBuffer.Reset();
                                if (processingConfig.Mode == Mode.Single)
                                    singleTriggerLatch = true;
                                StartHardware();
                                logger.LogDebug($"{nameof(ProcessingRunDto)}");
                                break;
                            case ProcessingStopDto processingStopDto:
                                StopHardware();
                                logger.LogDebug($"{nameof(ProcessingStopDto)}");
                                break;
                            case ProcessingForceDto processingForceDto:
                                if (runMode)        // FORCE is ignored if not in runMode.
                                {
                                    modeAfterForce = processingConfig.Mode;
                                    forceTriggerLatch = true;
                                }
                                logger.LogDebug($"{nameof(ProcessingForceDto)}");
                                break;
                            case ProcessingGetStateRequest processingGetStateRequest:
                                processingResponseChannel.Write(new ProcessingGetStateResponse(runMode));
                                logger.LogDebug($"{nameof(ProcessingGetStateRequest)}");
                                break;
                            case ProcessingGetModeRequest processingGetModeRequest:
                                processingResponseChannel.Write(new ProcessingGetModeResponse(processingConfig.Mode));
                                logger.LogDebug($"{nameof(ProcessingGetModeRequest)}");
                                break;
                            case ProcessingGetDepthRequest processingGetDepthRequest:
                                processingResponseChannel.Write(new ProcessingGetDepthResponse(processingConfig.ChannelDataLength));
                                logger.LogDebug($"{nameof(ProcessingGetDepthRequest)}");
                                break;
                            case ProcessingGetTriggerSourceRequest processingGetTriggerSourceRequest:
                                processingResponseChannel.Write(new ProcessingGetTriggerSourceResponse(processingConfig.TriggerChannel));
                                logger.LogDebug($"{nameof(ProcessingGetTriggerSourceRequest)}");
                                break;
                            case ProcessingGetTriggerTypeRequest processingGetTriggerTypeRequest:
                                processingResponseChannel.Write(new ProcessingGetTriggerTypeResponse(processingConfig.TriggerType));
                                logger.LogDebug($"{nameof(ProcessingGetTriggerTypeRequest)}");
                                break;
                            case ProcessingGetTriggerDelayRequest processingGetTriggerDelayRequest:
                                processingResponseChannel.Write(new ProcessingGetTriggerDelayResponse(processingConfig.TriggerDelayFs));
                                logger.LogDebug($"{nameof(ProcessingGetTriggerDelayRequest)}");
                                break;
                            case ProcessingGetTriggerHoldoffRequest processingGetTriggerHoldoffRequest:
                                processingResponseChannel.Write(new ProcessingGetTriggerHoldoffResponse(processingConfig.TriggerHoldoffFs));
                                logger.LogDebug($"{nameof(ProcessingGetTriggerHoldoffRequest)}");
                                break;
                            case ProcessingGetTriggerInterpolationRequest processingGetTriggerInterpolationRequest:
                                processingResponseChannel.Write(new ProcessingGetTriggerInterpolationResponse(processingConfig.TriggerInterpolation));
                                logger.LogDebug($"{nameof(ProcessingGetTriggerInterpolationRequest)}");
                                break;
                            case ProcessingGetEdgeTriggerLevelRequest processingGetEdgeTriggerLevelRequest:
                                // Convert the internal trigger level back to volts
                                var triggerChannelFrontend = cachedHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);
                                double levelVolts = (processingConfig.EdgeTriggerParameters.Level / 127.0) * (triggerChannelFrontend.ActualVoltFullScale / 2);
                                processingResponseChannel.Write(new ProcessingGetEdgeTriggerLevelResponse(levelVolts));
                                logger.LogDebug($"{nameof(ProcessingGetEdgeTriggerLevelRequest)}");
                                break;
                            case ProcessingGetEdgeTriggerDirectionRequest processingGetEdgeTriggerDirectionRequest:
                                processingResponseChannel.Write(new ProcessingGetEdgeTriggerDirectionResponse(processingConfig.EdgeTriggerParameters.Direction));
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
                                logger.LogDebug($"{nameof(ProcessingSetModeDto)} (mode: {processingConfig.Mode})");
                                break;
                            case ProcessingSetTriggerSourceDto processingSetTriggerSourceDto:
                                processingConfig.TriggerChannel = processingSetTriggerSourceDto.Channel;
                                captureBuffer.Reset();
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSourceDto)} (channel: {processingConfig.TriggerChannel})");
                                break;
                            case ProcessingSetTriggerTypeDto processingSetTriggerTypeDto:
                                if (processingConfig.TriggerType != processingSetTriggerTypeDto.Type)
                                {
                                    processingConfig.TriggerType = processingSetTriggerTypeDto.Type;

                                    SwitchTrigger();
                                    UpdateTriggerHorizontal(cachedHardwareConfig);

                                    captureBuffer.Reset();
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
                                    logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerLevelDto)} (level: {triggerLevel}, hysteresis: {processingConfig.EdgeTriggerParameters.Hysteresis})");
                                }
                                else
                                {
                                    logger.LogDebug($"{nameof(ProcessingSetEdgeTriggerLevelDto)} (no change)");
                                }
                                break;
                            case ProcessingSetTriggerInterpolationDto processingSetTriggerInterpolation:
                                processingConfig.TriggerInterpolation = processingSetTriggerInterpolation.Enabled;
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
                                logger.LogDebug($"{nameof(ProcessingSetBoxcarFilter)} (averages: {processingSetBoxcarFilter.Averages})");
                                break;
                            default:
                                logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                                break;
                        }
                    }

                    if (incomingDataChannel.TryRead(out var inputDataDto, 10, cancelToken))
                    {
                        totalDequeueCount++;

                        // Check for any hardware changes that require action
                        if (inputDataDto.HardwareConfig.SampleRateHz != cachedHardwareConfig.SampleRateHz)
                        {
                            UpdateTriggerHorizontal(inputDataDto.HardwareConfig);
                            captureBuffer.Reset();
                            logger.LogTrace("Hardware sample rate change ({sampleRateHz})", inputDataDto.HardwareConfig.SampleRateHz);
                        }

                        if (inputDataDto.HardwareConfig.EnabledChannelsCount() != cachedHardwareConfig.EnabledChannelsCount())
                        {
                            processingConfig.ChannelCount = inputDataDto.HardwareConfig.EnabledChannelsCount();
                            captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelDataLength, processingConfig.ChannelDataType);
                            logger.LogTrace("Hardware enabled channel change ({channelCount})", processingConfig.ChannelCount);
                        }

                        cachedHardwareConfig = inputDataDto.HardwareConfig;

                        switch (cachedHardwareConfig.AdcChannelMode)
                        {
                            case AdcChannelMode.Single:
                                inputDataDto.Memory.SpanI8.CopyTo(processingBufferI8);
                                // Finished with the memory, return it
                                memoryReturnChannel.Write(inputDataDto.Memory);
                                // Apply digital filtering if configured
                                //    Triggering happens after filtering.
                                //    Post-filter means the UI display will align with the trigger point.
                                //    Potentially allow the configuration of pre/post later.
                                if (processingConfig.BoxcarAveraging != BoxcarAveraging.None)
                                {
                                    throw new NotImplementedException();
                                }

                                // Temporary code to convert I8 ADC samples to I16
                                if (processingConfig.ChannelDataType == ThunderscopeDataType.I16)
                                {
                                    Scale.I8toI16(processingBufferI8, processingBufferI16);
                                    //movingAverageFilterI16.Process(processingBufferI16);
                                }

                                // Write to circular sample buffer
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        sampleBuffers[0].Write<sbyte>(processingBufferI8);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        sampleBuffers[0].Write<short>(processingBufferI16);
                                        break;
                                }
                                streamSampleCounter += processingBufferLength_1Ch;
                                break;
                            case AdcChannelMode.Dual:
                                ShuffleI8.TwoChannels(input: inputDataDto.Memory.SpanI8, output: processingBufferI8);
                                // Finished with the memory, return it
                                memoryReturnChannel.Write(inputDataDto.Memory);

                                // Temporary code to convert I8 ADC samples to I16
                                if (processingConfig.ChannelDataType == ThunderscopeDataType.I16)
                                {
                                    Scale.I8toI16(processingBufferI8, processingBufferI16);
                                }

                                // Write to circular sample buffer
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        sampleBuffers[0].Write<sbyte>(processingBufferI8_2Ch_1);
                                        sampleBuffers[1].Write<sbyte>(processingBufferI8_2Ch_2);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        sampleBuffers[0].Write<short>(processingBufferI16_2Ch_1);
                                        sampleBuffers[1].Write<short>(processingBufferI16_2Ch_2);
                                        break;
                                }
                                streamSampleCounter += processingBufferLength_2Ch;
                                break;
                            case AdcChannelMode.Quad:
                                // Quad channel mode is a bit different, it's processed as 4 channels but stored in the capture buffer as 3 or 4 channels.
                                ShuffleI8.FourChannels(input: inputDataDto.Memory.SpanI8, output: processingBufferI8);
                                // Finished with the memory, return it
                                memoryReturnChannel.Write(inputDataDto.Memory);

                                // Temporary code to convert I8 ADC samples to I16
                                if (processingConfig.ChannelDataType == ThunderscopeDataType.I16)
                                {
                                    Scale.I8toI16(processingBufferI8, processingBufferI16);
                                }

                                // Write to circular sample buffer
                                switch (processingConfig.ChannelDataType)
                                {
                                    case ThunderscopeDataType.I8:
                                        sampleBuffers[0].Write<sbyte>(processingBufferI8_4Ch_1);
                                        sampleBuffers[1].Write<sbyte>(processingBufferI8_4Ch_2);
                                        sampleBuffers[2].Write<sbyte>(processingBufferI8_4Ch_3);
                                        sampleBuffers[3].Write<sbyte>(processingBufferI8_4Ch_4);
                                        break;
                                    case ThunderscopeDataType.I16:
                                        sampleBuffers[0].Write<short>(processingBufferI16_4Ch_1);
                                        sampleBuffers[1].Write<short>(processingBufferI16_4Ch_2);
                                        sampleBuffers[2].Write<short>(processingBufferI16_4Ch_3);
                                        sampleBuffers[3].Write<short>(processingBufferI16_4Ch_4);
                                        break;
                                }
                                streamSampleCounter += shuffleBufferLength_4Ch;
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
                                        // Load in the trigger buffer from the correct shuffle buffer
                                        Span<sbyte> triggerChannelBufferI8;
                                        Span<short> triggerChannelBufferI16;
                                        int triggerChannelCaptureIndex;
                                        switch (cachedHardwareConfig.AdcChannelMode)
                                        {
                                            case AdcChannelMode.Single:
                                                triggerChannelCaptureIndex = 0;
                                                triggerChannelBufferI8 = processingBufferI8;
                                                triggerChannelBufferI16 = processingBufferI16;
                                                break;
                                            case AdcChannelMode.Dual:
                                                triggerChannelCaptureIndex = cachedHardwareConfig.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                                triggerChannelBufferI8 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => processingBufferI8_2Ch_1,
                                                    1 => processingBufferI8_2Ch_2,
                                                    _ => throw new NotImplementedException()
                                                };
                                                triggerChannelBufferI16 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => processingBufferI16_2Ch_1,
                                                    1 => processingBufferI16_2Ch_2,
                                                    _ => throw new NotImplementedException()
                                                };
                                                break;
                                            case AdcChannelMode.Quad:
                                                triggerChannelCaptureIndex = cachedHardwareConfig.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                                triggerChannelBufferI8 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => processingBufferI8_4Ch_1,
                                                    1 => processingBufferI8_4Ch_2,
                                                    2 => processingBufferI8_4Ch_3,
                                                    3 => processingBufferI8_4Ch_4,
                                                    _ => throw new NotImplementedException()
                                                };
                                                triggerChannelBufferI16 = triggerChannelCaptureIndex switch
                                                {
                                                    0 => processingBufferI16_4Ch_1,
                                                    1 => processingBufferI16_4Ch_2,
                                                    2 => processingBufferI16_4Ch_3,
                                                    3 => processingBufferI16_4Ch_4,
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
                    hardwareRequestChannel.Write(new HardwareStartRequest());
                }

                void StopHardware()
                {
                    runMode = false;
                    forceTriggerLatch = false;
                    hardwareRequestChannel.Write(new HardwareStopRequest());
                    // Wait for response before clearing incomingDataChannel
                    if (hardwareResponseChannel.TryRead(out var response, 500))
                    {
                        switch (response)
                        {
                            case HardwareStopResponse hardwareStopResponse:
                                int i = 0;
                                while (incomingDataChannel.TryRead(out var inputDataDto, 10, cancelToken))
                                {
                                    i++;
                                    memoryReturnChannel.Write(inputDataDto.Memory, cancelToken);
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
                                    sampleBuffers[b].Read(captureBuffer.GetChannelWriteBuffer<sbyte>(b), offset);
                                    break;
                                case ThunderscopeDataType.I16:
                                    sampleBuffers[b].Read(captureBuffer.GetChannelWriteBuffer<short>(b), offset);
                                    break;
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
                NativeMemory.AlignedFree(processingBufferI8P);
                NativeMemory.AlignedFree(processingBufferI16P);
            }
        }
    }
}
