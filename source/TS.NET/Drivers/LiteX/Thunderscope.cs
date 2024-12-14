using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX
{
    public class Thunderscope : IThunderscope
    {
        private readonly ILogger logger;
        private readonly uint readSegmentLengthBytes;

        private bool open = false;

        public Thunderscope(ILoggerFactory loggerFactory, int readSegmentLengthBytes)
        {
            logger = loggerFactory.CreateLogger("Driver.LiteX");
            if (ThunderscopeMemory.Length % readSegmentLengthBytes != 0)
                throw new ArgumentException("ThunderscopeMemory.Length % readSegmentLengthBytes != 0");
            this.readSegmentLengthBytes = (uint)readSegmentLengthBytes;
        }

        public void Open(ThunderscopeHardwareConfig initialHardwareConfig)
        {
            if (open)
                Close();

            //interop = ThunderscopeInterop.CreateInterop(device);
            //this.configuration = initialHardwareConfig;
            //this.revision = revision;

            //Initialise();
            open = true;
        }

        public void Close()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            open = false;

            // Disable data
        }

        public void Start()
        { 
        }

        public void Stop()
        { }

        public ThunderscopeChannelFrontend GetChannelFrontend(int channelIndex)
        { }

        public ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex)
        { }

        public void SetChannelEnable(int channelIndex, bool enabled)
        { }

        public void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel)
        { }

        public void SetChannelCalibration(int channelIndex, ThunderscopeChannelCalibration channelCalibration)
        { }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        { }

        public ThunderscopeHardwareConfig GetConfiguration()
        { }

        public void SetRate(ulong sampleRateHz)
        { }
    }
}
