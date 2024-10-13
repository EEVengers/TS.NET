using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;

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
        private readonly ChannelCaptureCircularBufferI8 captureBuffer;

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
            ChannelCaptureCircularBufferI8 captureBuffer)
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
        private static void Loop(
            ILogger logger,
            ThunderscopeSettings settings,
            ThunderscopeHardwareConfig cachedHardwareConfig,
            BlockingChannelReader<InputDataDto> processingDataChannel,
            BlockingChannelWriter<ThunderscopeMemory> inputChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel,
            SemaphoreSlim startSemaphore,
            ChannelCaptureCircularBufferI8 captureBuffer,
            CancellationToken cancelToken)
        {
            try
            {
                Thread.CurrentThread.Name = nameof(ProcessingThread);
                if (settings.ProcessingThreadProcessorAffinity > -1)
                {
                    Thread.BeginThreadAffinity();
                    OsThread.SetThreadAffinity(settings.ProcessingThreadProcessorAffinity);
                    logger.LogDebug($"{nameof(ProcessingThread)} thread processor affinity set to {settings.ProcessingThreadProcessorAffinity}");
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
                    AdcChannelMode.Single => 1
                };
                var processingConfig = new ThunderscopeProcessingConfig
                {
                    ChannelCount = initialChannelCount,
                    ChannelDataLength = settings.MaxChannelDataLength,
                    ChannelDataType = ThunderscopeDataType.I8,
                    TriggerChannel = TriggerChannel.Channel1,
                    TriggerMode = TriggerMode.Auto,
                    TriggerType = TriggerType.RisingEdge,
                    TriggerDelayFs = 0,
                    TriggerHoldoff = 0,
                    TriggerLevel = 0,
                    TriggerHysteresis = 5,
                    BoxcarAveraging = BoxcarAveraging.None
                };

                //bridge.Processing = processingConfig;

                // Reset monitoring
                //bridge.MonitoringReset();

                // Various buffers allocated once and reused forevermore.
                //Memory<byte> hardwareBuffer = new byte[ThunderscopeMemory.Length];
                // Shuffle buffers. Only needed for 2/4 channel modes.
                Span<sbyte> shuffleBuffer = new sbyte[ThunderscopeMemory.Length];
                // --2 channel buffers
                int blockLength_2Ch = (int)ThunderscopeMemory.Length / 2;
                Span<sbyte> shuffleBuffer2Ch_1 = shuffleBuffer.Slice(0, blockLength_2Ch);
                Span<sbyte> shuffleBuffer2Ch_2 = shuffleBuffer.Slice(blockLength_2Ch, blockLength_2Ch);
                // --4 channel buffers
                int blockLength_4Ch = (int)ThunderscopeMemory.Length / 4;
                Span<sbyte> shuffleBuffer4Ch_1 = shuffleBuffer.Slice(0, blockLength_4Ch);
                Span<sbyte> shuffleBuffer4Ch_2 = shuffleBuffer.Slice(blockLength_4Ch, blockLength_4Ch);
                Span<sbyte> shuffleBuffer4Ch_3 = shuffleBuffer.Slice(blockLength_4Ch * 2, blockLength_4Ch);
                Span<sbyte> shuffleBuffer4Ch_4 = shuffleBuffer.Slice(blockLength_4Ch * 3, blockLength_4Ch);
                Span<uint> captureEndIndices = new uint[ThunderscopeMemory.Length / 1000];  // 1000 samples is the minimum window width

                // Periodic debug display variables
                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                ulong totalDequeueCount = 0;
                ulong cachedTotalDequeueCount = 0;
                //ulong cachedBridgeWrites = 0;
                //ulong cachedBridgeReads = 0;

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

                IEdgeTriggerI8 edgeTriggerI8 = new RisingEdgeTriggerI8();
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

                    if (processingRequestChannel.TryRead(out var request, cancelToken))
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
                            case ProcessingSetDepthDto processingSetDepthDto:
                                if (processingConfig.ChannelDataLength != processingSetDepthDto.Samples)
                                {
                                    processingConfig.ChannelDataLength = processingSetDepthDto.Samples;
                                    captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelLengthBytes());
                                    UpdateTriggerHorizontalPosition(cachedHardwareConfig);
                                    logger.LogDebug($"{nameof(ProcessingSetDepthDto)} ({processingConfig.ChannelDataLength})");
                                }
                                else
                                {
                                    logger.LogDebug($"{nameof(ProcessingSetDepthDto)} (no change)");
                                }
                                break;
                            case ProcessingSetTriggerSourceDto processingSetTriggerSourceDto:
                                processingConfig.TriggerChannel = processingSetTriggerSourceDto.Channel;
                                captureBuffer.Reset();
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSourceDto)} (channel: {processingConfig.TriggerChannel})");
                                break;
                            case ProcessingSetTriggerDelayDto processingSetTriggerDelayDto:
                                if (processingConfig.TriggerDelayFs != processingSetTriggerDelayDto.Femtoseconds)
                                {
                                    processingConfig.TriggerDelayFs = processingSetTriggerDelayDto.Femtoseconds;
                                    UpdateTriggerHorizontalPosition(cachedHardwareConfig);
                                    captureBuffer.Reset();
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerDelayDto)} (femtoseconds: {processingConfig.TriggerDelayFs})");
                                }
                                else
                                {
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerDelayDto)} (no change)");
                                }
                                break;
                            case ProcessingSetTriggerLevelDto processingSetTriggerLevelDto:
                                var requestedTriggerLevel = processingSetTriggerLevelDto.LevelVolts;
                                // Convert the voltage to Int8

                                var triggerChannel = cachedHardwareConfig.GetTriggerChannelFrontend(processingConfig.TriggerChannel);

                                if ((requestedTriggerLevel > triggerChannel.ActualVoltFullScale / 2) || (requestedTriggerLevel < -triggerChannel.ActualVoltFullScale / 2))
                                {
                                    logger.LogWarning($"Could not set trigger level {requestedTriggerLevel}");
                                    break;
                                }

                                sbyte triggerLevel = (sbyte)((requestedTriggerLevel / (triggerChannel.ActualVoltFullScale / 2)) * 127f);
                                if (triggerLevel != processingConfig.TriggerLevel)
                                {
                                    processingConfig.TriggerLevel = triggerLevel;
                                    edgeTriggerI8.SetVertical(triggerLevel, (byte)processingConfig.TriggerHysteresis);
                                    captureBuffer.Reset();
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerLevelDto)} (level: {triggerLevel}, hysteresis: {processingConfig.TriggerHysteresis})");
                                }
                                else
                                {
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerLevelDto)} (no change)");
                                }
                                break;
                            case ProcessingSetTriggerTypeDto processingSetTriggerTypeDto:
                                if (processingConfig.TriggerType != processingSetTriggerTypeDto.Type)
                                {
                                    processingConfig.TriggerType = processingSetTriggerTypeDto.Type;

                                    edgeTriggerI8 = processingConfig.TriggerType switch
                                    {
                                        TriggerType.RisingEdge => new RisingEdgeTriggerI8(),
                                        TriggerType.FallingEdge => new FallingEdgeTriggerI8(),
                                        TriggerType.AnyEdge => new AnyEdgeTriggerI8(),
                                        _ => throw new NotImplementedException()
                                    };
                                    edgeTriggerI8.SetVertical((sbyte)processingConfig.TriggerLevel, (byte)processingConfig.TriggerHysteresis);
                                    UpdateTriggerHorizontalPosition(cachedHardwareConfig);

                                    captureBuffer.Reset();
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerTypeDto)} (type: {processingConfig.TriggerType})");
                                }
                                else
                                {
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerTypeDto)} (no change)");
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
                            UpdateTriggerHorizontalPosition(inputDataDto.HardwareConfig);
                            //for (int i = 0; i < 4; i++)
                            //{
                            //    sampleBuffers[i].Reset();
                            //}
                            captureBuffer.Reset();
                            logger.LogTrace("Hardware sample rate change ({0})", inputDataDto.HardwareConfig.SampleRateHz);
                        }

                        if (inputDataDto.HardwareConfig.EnabledChannelsCount() != cachedHardwareConfig.EnabledChannelsCount())
                        {
                            processingConfig.ChannelCount = inputDataDto.HardwareConfig.EnabledChannelsCount();
                            //for (int i = 0; i < 4; i++)
                            //{
                            //    sampleBuffers[i].Reset();
                            //}
                            captureBuffer.Configure(processingConfig.ChannelCount, processingConfig.ChannelLengthBytes());
                            logger.LogTrace("Hardware enabled channel change ({0})", processingConfig.ChannelCount);
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
                                streamSampleCounter += shuffleBuffer.Length;
                                break;
                            case AdcChannelMode.Dual:
                                ShuffleI8.TwoChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular sample buffers
                                sampleBuffers[0].Write(shuffleBuffer2Ch_1);
                                sampleBuffers[1].Write(shuffleBuffer2Ch_2);
                                streamSampleCounter += shuffleBuffer2Ch_1.Length;
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
                                streamSampleCounter += shuffleBuffer4Ch_1.Length;
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

                                        edgeTriggerI8.Process(input: triggerChannelBuffer, captureEndIndices: captureEndIndices, out uint captureEndCount);

                                        if (captureEndCount > 0)
                                        {
                                            for (int i = 0; i < captureEndCount; i++)
                                            {
                                                uint endOffset = (uint)triggerChannelBuffer.Length - captureEndIndices[i];
                                                TriggerCapture(triggered: true, triggerChannelCaptureIndex, endOffset);

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
                                        ForceCapture();

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
                void UpdateTriggerHorizontalPosition(ThunderscopeHardwareConfig hardwareConfig)
                {
                    ulong femtosecondsPerSample = 1000000000000000 / hardwareConfig.SampleRateHz;
                    var windowTriggerPosition = processingConfig.TriggerDelayFs / femtosecondsPerSample;
                    edgeTriggerI8.SetHorizontal((ulong)processingConfig.ChannelDataLength, windowTriggerPosition, processingConfig.TriggerHoldoff);
                }

                void TriggerCapture(bool triggered, int triggerChannelCaptureIndex, uint offset)
                {
                    if (captureBuffer.TryStartWrite())
                    {
                        int channelCount = cachedHardwareConfig.EnabledChannelsCount();
                        for (int b = 0; b < channelCount; b++)
                        {
                            sampleBuffers[b].Read(captureBuffer.GetWriteBuffer(b), offset);
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

                void ForceCapture()
                {
                    if (captureBuffer.TryStartWrite())
                    {
                        int channelCount = cachedHardwareConfig.EnabledChannelsCount();
                        for (int b = 0; b < channelCount; b++)
                        {
                            sampleBuffers[b].Read(captureBuffer.GetWriteBuffer(b), 0);
                        }
                        var captureMetadata = new CaptureMetadata
                        {
                            Triggered = false,
                            TriggerChannelCaptureIndex = 0,
                            HardwareConfig = cachedHardwareConfig,
                            ProcessingConfig = processingConfig
                        };
                        captureBuffer.FinishWrite(captureMetadata);
                    }
                }

                void StreamCapture()
                {
                    int channelCount = cachedHardwareConfig.EnabledChannelsCount();
                    int channelLength = processingConfig.ChannelDataLength;
                    while (streamSampleCounter > channelLength)
                    {
                        streamSampleCounter -= channelLength;
                        if (captureBuffer.TryStartWrite())
                        {
                            for (int b = 0; b < channelCount; b++)
                            {
                                sampleBuffers[b].Read(captureBuffer.GetWriteBuffer(b), 0);
                            }
                            var captureMetadata = new CaptureMetadata
                            {
                                Triggered = false,
                                TriggerChannelCaptureIndex = 0,
                                HardwareConfig = cachedHardwareConfig,
                                ProcessingConfig = processingConfig
                            };
                            captureBuffer.FinishWrite(captureMetadata);
                        }
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
            }
        }
    }
}
