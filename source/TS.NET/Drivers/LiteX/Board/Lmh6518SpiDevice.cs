using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX
{
    internal class Lmh6518SpiDevice : LiteSpiDevice
    {
        public Lmh6518SpiDevice(ILoggerFactory loggerFactory, LiteSpiBus spiBus, ushort address) : base(loggerFactory, spiBus, address)
        {

        }
    }
}
