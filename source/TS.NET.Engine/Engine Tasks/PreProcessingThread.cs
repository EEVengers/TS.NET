using Microsoft.Extensions.Logging;

namespace TS.NET.Engine
{
    public class PreProcessingThread : IEngineTask
    {
        private readonly ILogger logger;
        private readonly ThunderscopeSettings settings;
        private readonly BlockingPool<DataDto> hardwarePool;
        private readonly BlockingPool<DataDto> preProcessingPool;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public PreProcessingThread(
            ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            BlockingPool<DataDto> hardwarePool,
            BlockingPool<DataDto> preProcessingPool)
        {
            logger = loggerFactory.CreateLogger(nameof(PreProcessingThread));
            this.settings = settings;
            this.hardwarePool = hardwarePool;
            this.preProcessingPool = preProcessingPool;
        }

        public void Start(SemaphoreSlim startSemaphore)
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(
                logger: logger,
                settings: settings,
                hardwarePool: hardwarePool,
                preProcessingPool: preProcessingPool,
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
            BlockingPool<DataDto> hardwarePool,
            BlockingPool<DataDto> preProcessingPool,
            SemaphoreSlim startSemaphore,
            CancellationToken cancelToken)
        {
            try
            {
                Thread.CurrentThread.Name = "Pre-processing";
                if (settings.PreProcessingThreadProcessorAffinity > -1)
                {
                    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                    {
                        Thread.BeginThreadAffinity();
                        OsThread.SetThreadAffinity(settings.PreProcessingThreadProcessorAffinity);
                        logger.LogDebug($"{nameof(PreProcessingThread)} processor affinity set to {settings.PreProcessingThreadProcessorAffinity}");
                    }
                }

                logger.LogInformation("Started");
                startSemaphore.Release();

                //MovingAverageFilterI16 filter = new(8);
                //EmaFilterI16 filter = new(0.1f);
                //MovingAverageFilterI16_MT4 filter = new(8);
                //MovingAverageFilterI162 filter = new(8);
                //FirFilter filter = new FirFilter();
                //MovingAverageFilterI16_16Points filter = new();
                //FirFilter3 filter = new();
                //FirFilterI8toI16 filter = new();
                FirFilterI8toI16 filter = new();

                var preProcessingDataDto = preProcessingPool.Return.Reader.Read(cancelToken);
                preProcessingDataDto.Memory.Reset();
                bool validDataDto = true;

                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (!validDataDto)
                    {
                        preProcessingDataDto = preProcessingPool.Return.Reader.Read(cancelToken);
                        preProcessingDataDto.Memory.Reset();
                        validDataDto = true;
                    }

                    if (hardwarePool.Source.Reader.TryRead(out var hardwareDataDto, 10, cancelToken))
                    {
                        if (hardwareDataDto != null)
                        {
                            // Do shuffle operation for 2/4 channels
                            switch (hardwareDataDto.HardwareConfig.AdcChannelMode)
                            {
                                case AdcChannelMode.Single:
                                    hardwareDataDto.Memory.DataSpanI8.CopyTo(preProcessingDataDto.Memory.DataSpanI8);
                                    break;
                                case AdcChannelMode.Dual:
                                    ShuffleI8.TwoChannels(input: hardwareDataDto.Memory.DataSpanI8, output: preProcessingDataDto.Memory.DataSpanI8);
                                    break;
                                case AdcChannelMode.Quad:
                                    ShuffleI8.FourChannels(input: hardwareDataDto.Memory.DataSpanI8, output: preProcessingDataDto.Memory.DataSpanI8);
                                    break;
                            }
                            preProcessingDataDto.HardwareConfig = hardwareDataDto.HardwareConfig;
                            preProcessingDataDto.MemoryType = hardwareDataDto.MemoryType;


                            //Widen.I8toI16_NoScale(incomingDataDto.Memory.DataSpanI8, outgoingDataDto.Memory.DataSpanI16);
                            //outgoingDataDto.MemoryType = ThunderscopeDataType.I16;

                            //outgoingDataDto.Memory.PreambleUsedLength = filter.Process(ThunderscopeMemory.PreambleLength, outgoingDataDto.Memory.FullSpanI16);

                            hardwarePool.Return.Writer.Write(hardwareDataDto);
                            preProcessingPool.Source.Writer.Write(preProcessingDataDto);
                            validDataDto = false;
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
