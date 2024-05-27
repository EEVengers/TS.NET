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
                    BoxcarAveragingLength = 0,
                    
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

                // Trigger variables
                ulong cachedWindowTriggerPosition = 0;
                RisingEdgeTriggerI8_v2 risingEdgeTrigger = new(5, processingConfig.TriggerHysteresis, processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                FallingEdgeTriggerI8_v2 fallingEdgeTrigger = new(5, processingConfig.TriggerHysteresis, processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                

                DateTimeOffset startTime = DateTimeOffset.UtcNow;
                uint dequeueCounter = 0;
                uint oneSecondBridgeUpdateCount = 0;
                uint oneSecondDequeueCount = 0;
                // HorizontalSumUtility.ToDivisor(horizontalSumLength)
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

                bool runTrigger = true;
                bool forceTriggerLatch = false;     // "Latch" because it will reset state back to false automatically. If the force is invoked and a trigger happens anyway, it will be reset (effectively ignoring it and only updating the bridge once).
                bool singleTriggerLatch = false;    // "Latch" because it will reset state back to false automatically. When reset, runTrigger will be set to false.

                // Variables for Auto triggering
                Stopwatch autoTimeoutTimer = Stopwatch.StartNew();
                int autoSampleCounter = 0;
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
                                runTrigger = true;
                                logger.LogDebug($"Set Run");
                                break;
                            case ProcessingStopDto processingStopTriggerDto:
                                runTrigger = false;
                                logger.LogDebug($"Set Stop");
                                break;
                            case ProcessingForceTriggerDto processingForceTriggerDto:
                                forceTriggerLatch = true;
                                logger.LogDebug($"Set Force");
                                break;
                            case ProcessingSetTriggerModeDto processingSetTriggerModeDto:
                                processingConfig.TriggerMode = processingSetTriggerModeDto.Mode;
                                switch (processingSetTriggerModeDto.Mode)
                                {
                                    case TriggerMode.Normal:
                                        singleTriggerLatch = false;
                                        break;
                                    case TriggerMode.Single:
                                        singleTriggerLatch = true;
                                        break;
                                    case TriggerMode.Auto:
                                        autoTimeoutTimer.Restart();
                                        singleTriggerLatch = false;
                                        break;
                                }
                                logger.LogDebug($"Set TriggerMode to {processingConfig.TriggerMode}");
                                break;
                            case ProcessingSetDepthDto processingSetDepthDto:
                                processingConfig.CurrentChannelDataLength = processingSetDepthDto.Samples;
                                risingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                fallingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                logger.LogDebug($"Set CurrentChannelDataLength to {processingConfig.CurrentChannelDataLength}");
                                break;
                            case ProcessingSetRateDto processingSetRateDto:
                                var rate = processingSetRateDto.SamplingHz;
                                logger.LogWarning($"{nameof(ProcessingSetRateDto)} [Not implemented]");
                                break;
                            case ProcessingSetTriggerSourceDto processingSetTriggerSourceDto:
                                processingConfig.TriggerChannel = processingSetTriggerSourceDto.Channel;
                                logger.LogDebug($"Set TriggerChannel to {processingConfig.TriggerChannel}");
                                break;
                            case ProcessingSetTriggerDelayDto processingSetTriggerDelayDto:
                                processingConfig.TriggerDelayFs = processingSetTriggerDelayDto.Femtoseconds;
                                cachedWindowTriggerPosition = (ulong)Math.Floor(processingConfig.TriggerDelayFs / (1e15 / 250e6));
                                risingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                fallingEdgeTrigger.SetHorizontal(processingConfig.CurrentChannelDataLength, cachedWindowTriggerPosition, processingConfig.TriggerHoldoff);
                                logger.LogDebug($"Set trigger delay to {cachedWindowTriggerPosition} samples ({processingConfig.TriggerDelayFs} femtoseconds)");
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
                                logger.LogDebug($"Set trigger level to {triggerLevel} with hysteresis of {processingConfig.TriggerHysteresis}");
                                break;
                            case ProcessingSetTriggerTypeDto processingSetTriggerTypeDto:
                                processingConfig.TriggerType = processingSetTriggerTypeDto.Type;
                                logger.LogDebug($"Set TriggerType to {processingConfig.TriggerType}");
                                break;
                            default:
                                logger.LogWarning($"Unknown ProcessingRequestDto: {request}");
                                break;
                        }

                        bridge.Processing = processingConfig;
                    }

                    InputDataDto inputDataDto = processChannel.Read(cancelToken);
                    cachedThunderscopeConfiguration = inputDataDto.HardwareConfig;
                    bridge.Hardware = inputDataDto.HardwareConfig;
                    dequeueCounter++;
                    oneSecondDequeueCount++;

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
                            throw new NotImplementedException();
                            // Horizontal sum (EDIT: triggering should happen _before_ horizontal sum)
                            //if (config.HorizontalSumLength != HorizontalSumLength.None)
                            //    throw new NotImplementedException();
                            // Write to circular buffer
                            circularBuffer1.Write(inputDataDto.Memory.SpanI8);
                            // Trigger
                            if (processingConfig.TriggerChannel != TriggerChannel.None)
                            {
                                var triggerChannelBuffer = processingConfig.TriggerChannel switch
                                {
                                    TriggerChannel.One => inputDataDto.Memory.SpanI8,
                                    _ => throw new ArgumentException("Invalid TriggerChannel value")
                                };
                                //risingEdgeTrigger.ProcessSimd(input: triggerChannelBuffer, triggerIndices: triggerIndices, out uint triggerCount, holdoffEndIndices: holdoffEndIndices, out uint holdoffEndCount);
                            }
                            // Finished with the memory, return it
                            inputChannel.Write(inputDataDto.Memory);
                            break;
                        case AdcChannelMode.Dual:
                            throw new NotImplementedException();
                            // Shuffle
                            Shuffle.TwoChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                            // Finished with the memory, return it
                            inputChannel.Write(inputDataDto.Memory);
                            // Horizontal sum (EDIT: triggering should happen _before_ horizontal sum)
                            //if (config.HorizontalSumLength != HorizontalSumLength.None)
                            //    throw new NotImplementedException();
                            // Write to circular buffer
                            circularBuffer1.Write(postShuffleCh1_2);
                            circularBuffer2.Write(postShuffleCh2_2);
                            // Trigger
                            if (processingConfig.TriggerChannel != TriggerChannel.None)
                            {
                                var triggerChannelBuffer = processingConfig.TriggerChannel switch
                                {
                                    TriggerChannel.One => postShuffleCh1_2,
                                    TriggerChannel.Two => postShuffleCh2_2,
                                    _ => throw new ArgumentException("Invalid TriggerChannel value")
                                };
                                //risingEdgeTrigger.ProcessSimd(input: triggerChannelBuffer, triggerIndices: triggerIndices, out uint triggerCount, holdoffEndIndices: holdoffEndIndices, out uint holdoffEndCount);
                            }
                            break;
                        case AdcChannelMode.Quad:
                            // Shuffle
                            Shuffle.FourChannels(input: inputDataDto.Memory.SpanI8, output: shuffleBuffer);
                            // Finished with the memory, return it
                            inputChannel.Write(inputDataDto.Memory);
                            // Horizontal sum (EDIT: triggering should happen _before_ horizontal sum)
                            //if (config.HorizontalSumLength != HorizontalSumLength.None)
                            //    throw new NotImplementedException();
                            // Write to circular buffer
                            circularBuffer1.Write(postShuffleCh1_4);
                            circularBuffer2.Write(postShuffleCh2_4);
                            circularBuffer3.Write(postShuffleCh3_4);
                            circularBuffer4.Write(postShuffleCh4_4);
                            autoSampleCounter += postShuffleCh1_4.Length;
                            // Trigger
                            if (runTrigger && processingConfig.TriggerChannel != TriggerChannel.None)
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
                                        {
                                            risingEdgeTrigger.ProcessSimd(input: triggerChannelBuffer, captureEndIndices: captureEndIndices, out captureEndCount);
                                            break;
                                        }

                                    case TriggerType.FallingEdge:
                                        {
                                            fallingEdgeTrigger.ProcessSimd(input: triggerChannelBuffer, captureEndIndices: captureEndIndices, out captureEndCount);
                                            break;
                                        }
                                }


                                if (captureEndCount > 0)
                                {
                                    //logger.LogDebug("Trigger fired");
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
                                        oneSecondBridgeUpdateCount++;
                                    }
                                    autoSampleCounter = 0;
                                    autoTimeoutTimer.Restart();     // Restart the auto timeout as a normal trigger happened

                                    if (singleTriggerLatch)         // If this was a single trigger, reset the singleTrigger & runTrigger latches
                                    {
                                        singleTriggerLatch = false;
                                        runTrigger = false;
                                    }
                                }
                                else if (processingConfig.TriggerMode == TriggerMode.Auto && autoSampleCounter > channelLength && autoTimeoutTimer.ElapsedMilliseconds > autoTimeout)
                                {
                                    //logger.LogDebug("Auto trigger fired");
                                    autoSampleCounter -= channelLength;
                                    var bridgeSpan = bridge.AcquiringRegionI8;
                                    circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), 0);
                                    circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), 0);
                                    circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), 0);
                                    circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), 0);
                                    bridge.DataWritten();
                                    bridge.SwitchRegionIfNeeded();
                                    oneSecondBridgeUpdateCount++;
                                }
                                else
                                {
                                    bridge.SwitchRegionIfNeeded();  // To do: add a comment here when the reason for this LoC is discovered...!
                                }
                            }
                            if (forceTriggerLatch)  // This will always run, despite whether a trigger has happened or not (so from the user perspective, the UI might show one misaligned waveform during normal triggering; this is intended)
                            {
                                //logger.LogDebug("Force trigger fired");
                                var bridgeSpan = bridge.AcquiringRegionI8;
                                circularBuffer1.Read(bridgeSpan.Slice(0, channelLength), 0);        // TODO - work out if this should be zero? It probably wants to be a read of the oldest data + channelLength instead, to get 100% throughput.
                                circularBuffer2.Read(bridgeSpan.Slice(channelLength, channelLength), 0);
                                circularBuffer3.Read(bridgeSpan.Slice(channelLength + channelLength, channelLength), 0);
                                circularBuffer4.Read(bridgeSpan.Slice(channelLength + channelLength + channelLength, channelLength), 0);
                                bridge.DataWritten();
                                bridge.SwitchRegionIfNeeded();
                                oneSecondBridgeUpdateCount++;
                                forceTriggerLatch = false;

                                autoSampleCounter = 0;
                                autoTimeoutTimer.Restart();     // Restart the auto timeout as a force trigger happened
                            }

                            //logger.LogInformation($"Dequeue #{dequeueCounter++}, Ch1 triggers: {triggerCount1}, Ch2 triggers: {triggerCount2}, Ch3 triggers: {triggerCount3}, Ch4 triggers: {triggerCount4} ");
                            break;
                    }

                    if (periodicUpdateTimer.ElapsedMilliseconds >= 10000)
                    {
                        logger.LogDebug($"Outstanding frames: {processChannel.PeekAvailable()}, dequeues/sec: {oneSecondDequeueCount / (periodicUpdateTimer.Elapsed.TotalSeconds):F2}, dequeue count: {dequeueCounter}");
                        logger.LogDebug($"Triggers/sec: {oneSecondBridgeUpdateCount / (periodicUpdateTimer.Elapsed.TotalSeconds):F2}, trigger count: {bridge.Monitoring.TotalAcquisitions}, UI dropped triggers: {bridge.Monitoring.MissedAcquisitions}");
                        periodicUpdateTimer.Restart();
                        oneSecondBridgeUpdateCount = 0;
                        oneSecondDequeueCount = 0;
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
