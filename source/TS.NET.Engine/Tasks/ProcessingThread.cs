﻿using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TS.NET.Engine
{
    public class ProcessingThread
    {
        private readonly ILogger logger;
        private readonly ThunderscopeSettings settings;
        private readonly ThunderscopeHardwareConfig hardwareConfig;
        private readonly BlockingChannelReader<InputDataDto> processChannel;
        private readonly BlockingChannelWriter<ThunderscopeMemory> inputChannel;
        private readonly BlockingChannelReader<ProcessingRequestDto> processingRequestChannel;
        private readonly BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel;
        private readonly CaptureCircularBufferI8 captureBuffer;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public ProcessingThread(
            ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            ThunderscopeHardwareConfig hardwareConfig,
            BlockingChannelReader<InputDataDto> processChannel,
            BlockingChannelWriter<ThunderscopeMemory> inputChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel,
            CaptureCircularBufferI8 captureBuffer)
        {
            logger = loggerFactory.CreateLogger(nameof(ProcessingThread));
            this.settings = settings;
            this.hardwareConfig = hardwareConfig;
            this.processChannel = processChannel;
            this.inputChannel = inputChannel;
            this.processingRequestChannel = processingRequestChannel;
            this.processingResponseChannel = processingResponseChannel;
            this.captureBuffer = captureBuffer;
        }

        public void Start(SemaphoreSlim startSemaphore)
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, settings, hardwareConfig, processChannel, inputChannel, processingRequestChannel, processingResponseChannel, startSemaphore, captureBuffer, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
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
            BlockingChannelReader<InputDataDto> processingDataChannel,
            BlockingChannelWriter<ThunderscopeMemory> inputChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel,
            SemaphoreSlim startSemaphore,
            CaptureCircularBufferI8 captureBuffer,
            CancellationToken cancelToken)
        {

            const int memoryLength_1Ch = ThunderscopeMemory.Length;
            var shuffleBufferP = NativeMemory.AlignedAlloc(memoryLength_1Ch, 32);
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
                    ChannelDataLength = settings.MaxChannelDataLength,
                    ChannelDataType = ThunderscopeDataType.I8,
                    TriggerChannel = TriggerChannel.Channel1,
                    TriggerMode = TriggerMode.Auto,
                    TriggerType = TriggerType.Edge,
                    TriggerDelayFs = 0,
                    TriggerHoldoffFs = 0,
                    TriggerInterpolation = true,
                    EdgeTriggerParameters = new EdgeTriggerParameters() { Level = 0, Hysteresis = 5, Direction = EdgeDirection.Rising },
                    BurstTriggerParameters = new BurstTriggerParameters() { WindowHighLevel = 64, WindowLowLevel = -64, MinimumInRangePeriod = 450000 },
                    BoxcarAveraging = BoxcarAveraging.None
                };

                // Shuffle buffers. Only needed for 2/4 channel modes.
                Span<sbyte> shuffleBuffer = new Span<sbyte>((sbyte*)shuffleBufferP, memoryLength_1Ch);              
                //Span<sbyte> shuffleBuffer = new sbyte[memoryLength_1Ch];
                // --2 channel buffers
                const int memoryLength_2Ch = (int)ThunderscopeMemory.Length / 2;
                Span<sbyte> shuffleBuffer2Ch_1 = shuffleBuffer.Slice(0, memoryLength_2Ch);
                Span<sbyte> shuffleBuffer2Ch_2 = shuffleBuffer.Slice(memoryLength_2Ch, memoryLength_2Ch);
                // --4 channel buffers
                const int memoryLength_4Ch = (int)ThunderscopeMemory.Length / 4;
                Span<sbyte> shuffleBuffer4Ch_1 = shuffleBuffer.Slice(0, memoryLength_4Ch);
                Span<sbyte> shuffleBuffer4Ch_2 = shuffleBuffer.Slice(memoryLength_4Ch, memoryLength_4Ch);
                Span<sbyte> shuffleBuffer4Ch_3 = shuffleBuffer.Slice(memoryLength_4Ch * 2, memoryLength_4Ch);
                Span<sbyte> shuffleBuffer4Ch_4 = shuffleBuffer.Slice(memoryLength_4Ch * 3, memoryLength_4Ch);
                Span<int> captureEndIndices = new int[ThunderscopeMemory.Length / 1000];  // 1000 samples is the minimum window width

                // Periodic debug display variables
                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                ulong totalDequeueCount = 0;
                ulong cachedTotalDequeueCount = 0;

                Stopwatch periodicUpdateTimer = Stopwatch.StartNew();

                var sampleBuffers = new ChannelSampleCircularBufferI8[4];
                for (int i = 0; i < 4; i++)
                {
                    sampleBuffers[i] = new ChannelSampleCircularBufferI8((uint)(settings.MaxChannelDataLength + ThunderscopeMemory.Length));
                }
                captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelLengthBytes());

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
                bool runMode = true;
                bool forceTriggerLatch = false;     // "Latch" because it will reset state back to false. If the force is invoked and a trigger happens anyway, it will be reset (effectively ignoring it and only updating the bridge once).
                bool singleTriggerLatch = false;    // "Latch" because it will reset state back to false. When reset, runTrigger will be set to false.

                // Variables for Auto triggering
                Stopwatch autoTimeoutTimer = Stopwatch.StartNew();
                int streamSampleCounter = 0;
                long autoTimeout = 1000;

                logger.LogInformation("Started");
                startSemaphore.Release();

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (processingRequestChannel.TryRead(out var request))
                    {
                        switch (request)
                        {
                            case ProcessingRunDto processingStartTriggerDto:
                                runMode = true;
                                processingConfig.TriggerMode = TriggerMode.Auto;        // To do: cache the last setting of AUTO/NORMAL/STREAM and use it here
                                captureBuffer.Reset();
                                logger.LogDebug($"{nameof(ProcessingRunDto)}");
                                break;
                            case ProcessingStopDto processingStopTriggerDto:
                                runMode = false;
                                logger.LogDebug($"{nameof(ProcessingStopDto)}");
                                break;
                            case ProcessingForceTriggerDto processingForceTriggerDto:       // Force only works in RUN mode, and finishes in RUN
                                forceTriggerLatch = true;
                                captureBuffer.Reset();
                                logger.LogDebug($"{nameof(ProcessingForceTriggerDto)}");
                                break;
                            case ProcessingSetTriggerModeDto processingSetTriggerModeDto:
                                processingConfig.TriggerMode = processingSetTriggerModeDto.Mode;
                                switch (processingSetTriggerModeDto.Mode)
                                {
                                    case TriggerMode.Normal:
                                    case TriggerMode.Stream:
                                        singleTriggerLatch = false;
                                        break;
                                    case TriggerMode.Single:                                // Single works in both RUN/STOP, and finishes in STOP
                                        runMode = true;
                                        singleTriggerLatch = true;
                                        break;
                                    case TriggerMode.Auto:
                                        singleTriggerLatch = false;
                                        autoTimeoutTimer.Restart();
                                        break;
                                }
                                captureBuffer.Reset();
                                logger.LogDebug($"{nameof(ProcessingSetTriggerModeDto)} (mode: {processingConfig.TriggerMode})");
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
                                if(processingConfig.EdgeTriggerParameters.Direction != processingSetEdgeTriggerDirection.Edge)
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
                                    captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelLengthBytes());
                                    UpdateTriggerHorizontal(cachedHardwareConfig);
                                    logger.LogDebug($"{nameof(ProcessingSetDepthDto)} ({processingConfig.ChannelDataLength})");
                                }
                                else
                                {
                                    logger.LogDebug($"{nameof(ProcessingSetDepthDto)} (no change)");
                                }
                                break;
                            default:
                                logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                                break;
                        }
                    }

                    if (processingDataChannel.TryRead(out var inputDataDto, 10, cancelToken))
                    {
                        totalDequeueCount++;

                        // Check for any hardware changes that require action
                        if (inputDataDto.HardwareConfig.SampleRateHz != cachedHardwareConfig.SampleRateHz)
                        {
                            UpdateTriggerHorizontal(inputDataDto.HardwareConfig);
                            //for (int i = 0; i < 4; i++)
                            //{
                            //    sampleBuffers[i].Reset();
                            //}
                            captureBuffer.Reset();
                            logger.LogTrace("Hardware sample rate change ({sampleRateHz})", inputDataDto.HardwareConfig.SampleRateHz);
                        }

                        if (inputDataDto.HardwareConfig.EnabledChannelsCount() != cachedHardwareConfig.EnabledChannelsCount())
                        {
                            processingConfig.ChannelCount = inputDataDto.HardwareConfig.EnabledChannelsCount();
                            //for (int i = 0; i < 4; i++)
                            //{
                            //    sampleBuffers[i].Reset();
                            //}
                            captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelLengthBytes());
                            logger.LogTrace("Hardware enabled channel change ({channelCount})", processingConfig.ChannelCount);
                        }

                        cachedHardwareConfig = inputDataDto.HardwareConfig;

                        switch (cachedHardwareConfig.AdcChannelMode)
                        {
                            case AdcChannelMode.Single:
                                inputDataDto.Memory.SpanI8.CopyTo(shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular sample buffer
                                sampleBuffers[0].Write(shuffleBuffer);
                                streamSampleCounter += memoryLength_1Ch;
                                break;
                            case AdcChannelMode.Dual:
                                ShuffleI8.TwoChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular sample buffers
                                sampleBuffers[0].Write(shuffleBuffer2Ch_1);
                                sampleBuffers[1].Write(shuffleBuffer2Ch_2);
                                streamSampleCounter += memoryLength_2Ch;
                                break;
                            case AdcChannelMode.Quad:
                                // Quad channel mode is a bit different, it's processed as 4 channels but stored in the capture buffer as 3 or 4 channels.
                                ShuffleI8.FourChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular sample buffers
                                sampleBuffers[0].Write(shuffleBuffer4Ch_1);
                                sampleBuffers[1].Write(shuffleBuffer4Ch_2);
                                sampleBuffers[2].Write(shuffleBuffer4Ch_3);
                                sampleBuffers[3].Write(shuffleBuffer4Ch_4);
                                streamSampleCounter += memoryLength_4Ch;
                                break;
                        }

                        if (runMode)
                        {
                            switch (processingConfig.TriggerMode)
                            {
                                case TriggerMode.Normal:
                                case TriggerMode.Single:
                                case TriggerMode.Auto:
                                    if (cachedHardwareConfig.IsTriggerChannelAnEnabledChannel(processingConfig.TriggerChannel))
                                    {
                                        // Load in the trigger buffer from the correct shuffle buffer
                                        Span<sbyte> triggerChannelBuffer;
                                        int triggerChannelCaptureIndex;
                                        switch (cachedHardwareConfig.AdcChannelMode)
                                        {
                                            case AdcChannelMode.Single:
                                                triggerChannelCaptureIndex = 0;
                                                triggerChannelBuffer = shuffleBuffer;
                                                break;
                                            case AdcChannelMode.Dual:
                                                triggerChannelCaptureIndex = cachedHardwareConfig.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);
                                                triggerChannelBuffer = triggerChannelCaptureIndex switch
                                                {
                                                    0 => shuffleBuffer2Ch_1,
                                                    1 => shuffleBuffer2Ch_2,
                                                    _ => throw new NotImplementedException()
                                                };
                                                break;
                                            case AdcChannelMode.Quad:
                                                triggerChannelCaptureIndex = cachedHardwareConfig.GetCaptureBufferIndexForTriggerChannel(processingConfig.TriggerChannel);

                                                // Bodge for mismatch in driver behaviour
                                                triggerChannelBuffer = liteX switch
                                                {
                                                    false => processingConfig.TriggerChannel switch
                                                    {
                                                        TriggerChannel.Channel1 => shuffleBuffer4Ch_1,
                                                        TriggerChannel.Channel2 => shuffleBuffer4Ch_2,
                                                        TriggerChannel.Channel3 => shuffleBuffer4Ch_3,
                                                        TriggerChannel.Channel4 => shuffleBuffer4Ch_4,
                                                        _ => throw new NotImplementedException()
                                                    },
                                                    true => triggerChannelCaptureIndex switch
                                                    {
                                                        0 => shuffleBuffer4Ch_1,
                                                        1 => shuffleBuffer4Ch_2,
                                                        2 => shuffleBuffer4Ch_3,
                                                        3 => shuffleBuffer4Ch_4,
                                                        _ => throw new NotImplementedException()
                                                    }
                                                };
                                                break;
                                            default:
                                                throw new NotImplementedException();
                                        }

                                        triggerI8.Process(input: triggerChannelBuffer, captureEndIndices: captureEndIndices, out int captureEndCount);

                                        if (captureEndCount > 0)
                                        {
                                            for (int i = 0; i < captureEndCount; i++)
                                            {
                                                int offset = triggerChannelBuffer.Length - captureEndIndices[i];
                                                Capture(triggered: true, triggerChannelCaptureIndex, offset);

                                                if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                                {
                                                    singleTriggerLatch = false;
                                                    runMode = false;
                                                    break;
                                                }
                                            }
                                            streamSampleCounter = 0;
                                            autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened
                                        }
                                    }
                                    if (forceTriggerLatch) // This will always run, despite whether a trigger has happened or not (so from the user perspective, the UI might show one misaligned waveform during normal triggering; this is intended)
                                    {
                                        Capture(triggered: false, triggerChannelCaptureIndex: 0, offset: 0);

                                        forceTriggerLatch = false;

                                        streamSampleCounter = 0;
                                        autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened
                                    }
                                    else if (processingConfig.TriggerMode == TriggerMode.Auto && autoTimeoutTimer.ElapsedMilliseconds > autoTimeout)
                                    {
                                        StreamCapture();
                                    }
                                    break;
                                case TriggerMode.Stream:
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
                }

                void UpdateTriggerHorizontal(ThunderscopeHardwareConfig hardwareConfig)
                {
                    ulong femtosecondsPerSample = 1000000000000000 / hardwareConfig.SampleRateHz;
                    long windowTriggerPosition = (long)(processingConfig.TriggerDelayFs / femtosecondsPerSample);
                    long additionalHoldoff = (long)(processingConfig.TriggerHoldoffFs / femtosecondsPerSample);
                    logger.LogTrace($"{additionalHoldoff}");
                    triggerI8.SetHorizontal(processingConfig.ChannelDataLength, windowTriggerPosition, additionalHoldoff);
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
                    }
                }

                void Capture(bool triggered, int triggerChannelCaptureIndex, int offset)
                {
                    if (captureBuffer.TryStartWrite())
                    {
                        int channelCount = cachedHardwareConfig.EnabledChannelsCount();
                        for (int b = 0; b < channelCount; b++)
                        {
                            sampleBuffers[b].Read(captureBuffer.GetWriteBuffer(b), (uint)offset);
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
                NativeMemory.AlignedFree(shuffleBufferP);
            }
        }
    }
}
