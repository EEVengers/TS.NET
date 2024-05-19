using Microsoft.Extensions.Logging;

namespace TS.NET.Engine
{
    internal class ControlThread
    {
        private readonly ILogger logger;
        private readonly ThunderscopeSettings settings;
        private readonly BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel;

        private CancellationTokenSource? cancelTokenSource;
        private Task? taskLoop;

        public ControlThread(ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel)
        {
            logger = loggerFactory.CreateLogger(nameof(ControlThread));
            this.settings = settings;
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.processingRequestChannel = processingRequestChannel;
        }

        public void Start()
        {
            cancelTokenSource = new CancellationTokenSource();
            taskLoop = Task.Factory.StartNew(() => Loop(logger, settings, hardwareRequestChannel, processingRequestChannel, cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            cancelTokenSource?.Cancel();
            taskLoop?.Wait();
        }

        private static void Loop(
            ILogger logger,
            ThunderscopeSettings settings,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            CancellationToken cancelToken)
        {
            Thread.CurrentThread.Name = nameof(ControlThread);
            if (settings.ControlThreadProcessorAffinity > -1 && OperatingSystem.IsWindows())
            {
                Thread.BeginThreadAffinity();
                Interop.CurrentThread.ProcessorAffinity = new IntPtr(1 << settings.ControlThreadProcessorAffinity);
                logger.LogDebug($"{nameof(ControlThread)} thread processor affinity set to {settings.ControlThreadProcessorAffinity}");
            }

            ThunderscopeDataBridgeConfig dataBridgeConfig = new()
            {
                MaxChannelCount = settings.MaxChannelCount,
                MaxChannelDataLength = settings.MaxChannelDataLength,
                ChannelDataType = ThunderscopeChannelDataType.I8
            };
            ThunderscopeControlBridgeReader controlBridge = new("ThunderScope.1", dataBridgeConfig);

            try
            {
                logger.LogInformation("Started");
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    Thread.Sleep(100);
                    //if(controlBridge.WaitForData(500))
                    //{
                    //    logger.LogDebug("Control request occurred");

                    //}
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
