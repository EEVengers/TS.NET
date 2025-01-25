using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX
{
    internal class LiteI2cDevice
    {
        private readonly ILogger logger;
        private readonly LiteI2cBus i2cBus;
        private readonly ushort address;

        public LiteI2cDevice(ILoggerFactory loggerFactory, LiteI2cBus i2cBus, ushort address)
        {
            logger = loggerFactory.CreateLogger(nameof(LiteI2cDevice));
            this.i2cBus = i2cBus;
            this.address = address;
        }
    }
}
