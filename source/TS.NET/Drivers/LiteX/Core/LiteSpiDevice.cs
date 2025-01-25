using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX
{
    internal class LiteSpiDevice
    {
        private readonly ILogger logger;
        private readonly LiteSpiBus spiBus;
        private readonly uint csIndex;

        public LiteSpiDevice(ILoggerFactory loggerFactory, LiteSpiBus spiBus, uint csIndex)
        {
            logger = loggerFactory.CreateLogger(nameof(LiteSpiDevice));
            this.spiBus = spiBus;
            this.csIndex = csIndex;
        }

        public void Write(byte register, ReadOnlySpan<byte> data)
        {
            spiBus.Write(register, data, csIndex);
        }

        public void WaitForBusIdle(double timeoutSec = 0.1)
        {
            spiBus.WaitForIdle(timeoutSec);
        }
    }
}
