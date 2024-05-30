using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TS.NET.Engine
{
    public class ProcessingThread
    {
        private readonly ILogger logger;
        private readonly ThunderscopeSettings settings;
        private readonly BlockingChannelReader<InputDataDto> processChannel;
        private readonly BlockingChannelWriter<ThunderscopeMemory> inputChannel;
        private readonly BlockingChannelReader<ProcessingRequestDto> processingRequestChannel;
        private readonly BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public ProcessingThread(
            ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            BlockingChannelReader<InputDataDto> processChannel,
            BlockingChannelWriter<ThunderscopeMemory> inputChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel)
        {
            logger = loggerFactory.CreateLogger(nameof(ProcessingThread));
            this.settings = settings;
            this.processChannel = processChannel;
            this.inputChannel = inputChannel;
            this.processingRequestChannel = processingRequestChannel;
            this.processingResponseChannel = processingResponseChannel;
        }

        public void Start()
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, settings, processChannel, inputChannel, processingRequestChannel, processingResponseChannel, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
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
            BlockingChannelReader<InputDataDto> processChannel,
            BlockingChannelWriter<ThunderscopeMemory> inputChannel,
            BlockingChannelReader<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelWriter<ProcessingResponseDto> processingResponseChannel,
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

                ThunderscopeDataBridgeConfig bridgeConfig = new()
                {
                    MaxChannelCount = settings.MaxChannelCount,
                    MaxChannelDataLength = settings.MaxChannelDataLength,
                    ChannelDataType = ThunderscopeChannelDataType.I8
                };
                ThunderscopeDataBridgeWriter bridge = new("ThunderScope.1", bridgeConfig);

                ThunderscopeHardwareConfig cachedThunderscopeConfiguration = default;

                // Set some sensible defaults
                var processingConfig = new ThunderscopeProcessingConfig
                {
                    CurrentChannelCount = settings.MaxChannelCount,
                    CurrentChannelDataLength = settings.MaxChannelDataLength,
                    TriggerChannel = TriggerChannel.One,
                    TriggerMode = TriggerMode.Auto,
                    TriggerType = TriggerType.RisingEdge,
                    TriggerDelayFs = 0,
                    TriggerHysteresis = 5,
                    TriggerHoldoff = 0,
                    BoxcarAveraging = BoxcarAveraging.None
                    
                };
                bridge.Processing = processingConfig;

                // Reset monitoring
                bridge.MonitoringReset();

                // Various buffers allocated once and reused forevermore.
                //Memory<byte> hardwareBuffer = new byte[ThunderscopeMemory.Length];
                // Shuffle buffers. Only needed for 2/4 channel modes.
                Span<sbyte> shuffleBuffer = new sbyte[ThunderscopeMemory.Length];
                // --2 channel buffers
                int blockLength_2 = (int)ThunderscopeMemory.Length / 2;
                Span<sbyte> postShuffleCh1_2 = shuffleBuffer.Slice(0, blockLength_2);
                Span<sbyte> postShuffleCh2_2 = shuffleBuffer.Slice(blockLength_2, blockLength_2);
                // --4 channel buffers
                int blockLength_4 = (int)ThunderscopeMemory.Length / 4;
                Span<sbyte> postShuffleCh1_4 = shuffleBuffer.Slice(0, blockLength_4);
                Span<sbyte> postShuffleCh2_4 = shuffleBuffer.Slice(blockLength_4, blockLength_4);
                Span<sbyte> postShuffleCh3_4 = shuffleBuffer.Slice(blockLength_4 * 2, blockLength_4);
                Span<sbyte> postShuffleCh4_4 = shuffleBuffer.Slice(blockLength_4 * 3, blockLength_4);              
                Span<uint> captureEndIndices = new uint[ThunderscopeMemory.Length / 1000];  // 1000 samples is the minimum window width

                // Periodic debug display variables
                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                ulong totalDequeueCount = 0;
                ulong cachedTotalDequeueCount = 0;
                ulong cachedTotalAcquisitions = 0;
                ulong cachedMissedAcquisitions = 0;
                Stopwatch periodicUpdateTimer = Stopwatch.StartNew();

                var circularBuffer1 = new ChannelCircularAlignedBufferI8((uint)processingConfig.CurrentChannelDataLength + ThunderscopeMemory.Length);
                var circularBuffer2 = new ChannelCircularAlignedBufferI8((uint)processingConfig.CurrentChannelDataLength + ThunderscopeMemory.Length);
                var circularBuffer3 = new ChannelCircularAlignedBufferI8((uint)processingConfig.CurrentChannelDataLength + ThunderscopeMemory.Length);
                var circularBuffer4 = new ChannelCircularAlignedBufferI8((uint)processingConfig.CurrentChannelDataLength + ThunderscopeMemory.Length);

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

                ulong cachedWindowTriggerPosition = 0;
                RisingEdgeTriggerI8_v2 risingEdgeTrigger = new(5, processingConfig.TriggerHysteresis, processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                FallingEdgeTriggerI8_v2 fallingEdgeTrigger = new(5, processingConfig.TriggerHysteresis, processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                bool runMode = true;
                bool forceTriggerLatch = false;     // "Latch" because it will reset state back to false. If the force is invoked and a trigger happens anyway, it will be reset (effectively ignoring it and only updating the bridge once).
                bool singleTriggerLatch = false;    // "Latch" because it will reset state back to false. When reset, runTrigger will be set to false.

                // Variables for Auto triggering
                Stopwatch autoTimeoutTimer = Stopwatch.StartNew();
                int streamSampleCounter = 0;
                long autoTimeout = 500;

                logger.LogInformation("Started");

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    // Check for processing requests
                    if (processingRequestChannel.TryRead(out var request))
                    {
                        switch (request)
                        {
                            case ProcessingRunDto processingStartTriggerDto:
                                runMode = true;
                                logger.LogDebug($"{nameof(ProcessingRunDto)}");
                                break;
                            case ProcessingStopDto processingStopTriggerDto:
                                runMode = false;
                                logger.LogDebug($"{nameof(ProcessingStopDto)}");
                                break;
                            case ProcessingForceTriggerDto processingForceTriggerDto:
                                forceTriggerLatch = true;
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
                                    case TriggerMode.Single:
                                        singleTriggerLatch = true;
                                        break;
                                    case TriggerMode.Auto:
                                        singleTriggerLatch = false;
                                        autoTimeoutTimer.Restart();
                                        break;
                                }
                                logger.LogDebug($"{nameof(ProcessingSetTriggerModeDto)} (mode: {processingConfig.TriggerMode})");
                                break;
                            case ProcessingSetDepthDto processingSetDepthDto:
                                processingConfig.CurrentChannelDataLength = processingSetDepthDto.Samples;
                                risingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                fallingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                logger.LogDebug($"{nameof(ProcessingSetDepthDto)} ({processingConfig.CurrentChannelDataLength})");
                                break;
                            case ProcessingSetRateDto processingSetRateDto:
                                var rate = processingSetRateDto.SamplingHz;
                                logger.LogWarning($"{nameof(ProcessingSetRateDto)} [Not implemented]");
                                break;
                            case ProcessingSetTriggerSourceDto processingSetTriggerSourceDto:
                                processingConfig.TriggerChannel = processingSetTriggerSourceDto.Channel;
                                logger.LogDebug($"{nameof(ProcessingSetTriggerSourceDto)} (channel: {processingConfig.TriggerChannel})");
                                break;
                            case ProcessingSetTriggerDelayDto processingSetTriggerDelayDto:
                                processingConfig.TriggerDelayFs = processingSetTriggerDelayDto.Femtoseconds;
                                cachedWindowTriggerPosition = (ulong)Math.Floor(processingConfig.TriggerDelayFs / (1e15 / 250e6));
                                risingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                fallingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                logger.LogDebug($"{nameof(ProcessingSetTriggerDelayDto)} (samples: {cachedWindowTriggerPosition}, femtoseconds: {processingConfig.TriggerDelayFs})");
                                break;
                            case ProcessingSetTriggerLevelDto processingSetTriggerLevelDto:
                                var requestedTriggerLevel = processingSetTriggerLevelDto.LevelVolts;
                                // Convert the voltage to Int8

                                var triggerChannel = cachedThunderscopeConfiguration.GetTriggerChannel(processingConfig.TriggerChannel);

                                if ((requestedTriggerLevel > triggerChannel.ActualVoltFullScale / 2) || (requestedTriggerLevel < -triggerChannel.ActualVoltFullScale / 2))
                                {
                                    logger.LogWarning($"Could not set trigger level {requestedTriggerLevel}");
                                    break;
                                }

                                sbyte triggerLevel = (sbyte)((requestedTriggerLevel / (triggerChannel.ActualVoltFullScale / 2)) * 127f);
                                risingEdgeTrigger.SetVertical(triggerLevel, processingConfig.TriggerHysteresis);
                                fallingEdgeTrigger.SetVertical(triggerLevel, processingConfig.TriggerHysteresis);
                                logger.LogDebug($"{nameof(ProcessingSetTriggerLevelDto)} (level: {triggerLevel}, hysteresis: {processingConfig.TriggerHysteresis})");
                                break;
                            case ProcessingSetTriggerTypeDto processingSetTriggerTypeDto:
                                processingConfig.TriggerType = processingSetTriggerTypeDto.Type;
                                logger.LogDebug($"{nameof(ProcessingSetTriggerTypeDto)} (type: {processingConfig.TriggerType})");
                                break;
                            case ProcessingGetRateRequestDto processingGetRateRequestDto:
                                    logger.LogDebug($"{nameof(ProcessingGetRateRequestDto)}");
                                    switch(cachedThunderscopeConfiguration.AdcChannelMode)
                                    {
                                        case AdcChannelMode.Single:
                                            processingResponseChannel.Write(new ProcessingGetRateResponseDto(1000000000));
                                            break;
                                        case AdcChannelMode.Dual:
                                            processingResponseChannel.Write(new ProcessingGetRateResponseDto(500000000));
                                            break;
                                        case AdcChannelMode.Quad:
                                            processingResponseChannel.Write(new ProcessingGetRateResponseDto(250000000));
                                            break;   
                                    }
                                    logger.LogDebug($"{nameof(ProcessingGetRateResponseDto)}");
                                    break;
                            default:
                                logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                                break;
                        }

                        bridge.Processing = processingConfig;
                    }

                    if(processChannel.TryRead(out InputDataDto inputDataDto, 10, cancelToken))
                    {
                        cachedThunderscopeConfiguration = inputDataDto.HardwareConfig;
                        bridge.Hardware = inputDataDto.HardwareConfig;
                        totalDequeueCount++;

                        int channelLength = (int)processingConfig.CurrentChannelDataLength;
                        switch (inputDataDto.HardwareConfig.AdcChannelMode)
                        {
                            // Processing pipeline:
                            // Shuffle (if needed)
                            // Horizontal sum (EDIT: triggering should happen _before_ horizontal sum)
                            // Write to circular buffer
                            // Trigger
                            // Data segment on trigger (if needed)
                            case AdcChannelMode.Single:
                                // Copy
                                inputDataDto.Memory.SpanI8.CopyTo(shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular buffer
                                circularBuffer1.Write(shuffleBuffer);
                                // Trigger
                                //throw new NotImplementedException();
                                break;
                            case AdcChannelMode.Dual:                            
                                // Shuffle
                                Shuffle.TwoChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular buffer
                                circularBuffer1.Write(postShuffleCh1_2);
                                circularBuffer2.Write(postShuffleCh2_2);                           
                                // Trigger
                                //throw new NotImplementedException();
                                break;
                            case AdcChannelMode.Quad:
                                // Shuffle
                                Shuffle.FourChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                                // Finished with the memory, return it
                                inputChannel.Write(inputDataDto.Memory);
                                // Write to circular buffer
                                circularBuffer1.Write(postShuffleCh1_4);
                                circularBuffer2.Write(postShuffleCh2_4);
                                circularBuffer3.Write(postShuffleCh3_4);
                                circularBuffer4.Write(postShuffleCh4_4);
                                streamSampleCounter += postShuffleCh1_4.Length;
                                // Trigger
                                if (runMode)
                                {
                                    switch(processingConfig.TriggerMode)
                                    {
                                        case TriggerMode.Normal:
                                        case TriggerMode.Single:
                                        case TriggerMode.Auto:
                                            if(processingConfig.TriggerChannel != TriggerChannel.None)
                                            {
                                                var triggerChannelBuffer = processingConfig.TriggerChannel switch
                                                {
                                                    TriggerChannel.One => postShuffleCh1_4,
                                                    TriggerChannel.Two => postShuffleCh2_4,
                                                    TriggerChannel.Three => postShuffleCh3_4,
                                                    TriggerChannel.Four => postShuffleCh4_4,
                                                    _ => throw new ArgumentException("Invalid TriggerChannel value")
                                                };

                                                uint captureEndCount = 0;
                                                switch (processingConfig.TriggerType)
                                                {
                                                    case TriggerType.RisingEdge:
                                                        risingEdgeTrigger.ProcessSimd(input: triggerChannelBuffer, captureEndIndices: captureEndIndices, out captureEndCount);
                                                        break;
                                                    case TriggerType.FallingEdge:
                                                        fallingEdgeTrigger.ProcessSimd(input: triggerChannelBuffer, captureEndIndices: captureEndIndices, out captureEndCount);
                                                        break;
                                                }

                                                if (captureEndCount > 0)
                                                {
                                                    for (int i = 0; i < captureEndCount; i++)
                                                    {
                                                        var bridgeSpan = bridge.AcquiringRegionI8;
                                                        uint endOffset = (uint)postShuffleCh1_4.Length - captureEndIndices[i];
                                                        circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), endOffset);
                                                        circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), endOffset);
                                                        circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), endOffset);
                                                        circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), endOffset);
                                                        bridge.DataWritten();
                                                        bridge.SwitchRegionIfNeeded();
                                                    }
                                                    streamSampleCounter = 0;
                                                    autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened

                                                    if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                                    {
                                                        singleTriggerLatch = false;
                                                        runMode = false;
                                                    }
                                                }
                                            }
                                            if (forceTriggerLatch) // This will always run, despite whether a trigger has happened or not (so from the user perspective, the UI might show one misaligned waveform during normal triggering; this is intended)
                                            {
                                                var bridgeSpan = bridge.AcquiringRegionI8;
                                                circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), 0);        // TODO - work out if this should be zero? It probably wants to be a read of the oldest data + channelLength instead, to get 100% throughput.
                                                circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), 0);
                                                circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), 0);
                                                circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), 0);
                                                bridge.DataWritten();
                                                bridge.SwitchRegionIfNeeded();
                                                forceTriggerLatch = false;

                                                streamSampleCounter = 0;
                                                autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened
                                            }
                                            if (processingConfig.TriggerMode == TriggerMode.Auto && autoTimeoutTimer.ElapsedMilliseconds > autoTimeout)
                                            {
                                                while(streamSampleCounter > channelLength)
                                                {
                                                    streamSampleCounter -= channelLength;
                                                    var bridgeSpan = bridge.AcquiringRegionI8;
                                                    circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), 0);        // TODO - work out if this should be zero?
                                                    circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), 0);
                                                    circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), 0);
                                                    circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), 0);
                                                    bridge.DataWritten();
                                                    bridge.SwitchRegionIfNeeded();
                                                }
                                            }
                                            break;
                                        case TriggerMode.Stream:
                                            while (streamSampleCounter > channelLength)
                                            {
                                                streamSampleCounter -= channelLength;
                                                var bridgeSpan = bridge.AcquiringRegionI8;
                                                circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), 0);        // TODO - work out if this should be zero?
                                                circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), 0);
                                                circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), 0);
                                                circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), 0);
                                                bridge.DataWritten();
                                                bridge.SwitchRegionIfNeeded();
                                            }
                                            break;
                                    }
                                }
                                //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}, Ch3 triggers: {triggerCount3}, Ch4 triggers: {triggerCount4} ");
                                break;
                        }
                        bridge.SwitchRegionIfNeeded();      // This ensures the semaphore is serviced on every loop iteration
                        if (periodicUpdateTimer.ElapsedMilliseconds >= 10000)
                    {
                        var dequeueCount = totalDequeueCount - cachedTotalDequeueCount;
                        var totalAcquisitions = bridge.Monitoring.TotalAcquisitions - cachedTotalAcquisitions;
                        var missedAcquisitions = bridge.Monitoring.DroppedAcquisitions - cachedMissedAcquisitions;
                        var uiUpdates = totalAcquisitions - missedAcquisitions;
                        
                        //logger.LogDebug($"Outstanding frames: {processChannel.PeekAvailable()}, dequeues/sec: {dequeueCount / periodicUpdateTimer.Elapsed.TotalSeconds:F2}, dequeue count: {totalDequeueCount}");
                        logger.LogDebug($"Acquisitions/sec: {totalAcquisitions / periodicUpdateTimer.Elapsed.TotalSeconds:F2}, UI updates/sec: {uiUpdates / periodicUpdateTimer.Elapsed.TotalSeconds:F2}, acquisitions: {bridge.Monitoring.TotalAcquisitions}");
                        periodicUpdateTimer.Restart();

                        cachedTotalDequeueCount = totalDequeueCount;
                        cachedTotalAcquisitions = bridge.Monitoring.TotalAcquisitions;
                        cachedMissedAcquisitions = bridge.Monitoring.DroppedAcquisitions;
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
