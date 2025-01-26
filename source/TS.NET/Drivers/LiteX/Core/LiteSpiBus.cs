using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX
{
    internal class LiteSpiBus
    {
        private readonly ILogger logger;
        private readonly LitePcie litePci;
        private readonly uint spiBaseAddress;
        private readonly uint numCs;

        const uint SPI_CS_MODE_NORMAL = 0;
        const uint SPI_CTRL_START = (1 << 0);

        public LiteSpiBus(ILoggerFactory loggerFactory, LitePcie litePci, uint spiBaseAddress, uint numCs)
        {
            logger = loggerFactory.CreateLogger($"{nameof(LiteSpiBus)} (baseAddress: {spiBaseAddress})");
            this.litePci = litePci;
            this.spiBaseAddress = spiBaseAddress;
            this.numCs = numCs;
        }

        // SPI devices on ThunderScope are write-only or single pin SDIO, so no WriteRead method
        public void Write(byte register, ReadOnlySpan<byte> data, uint csIndex)
        {
            if (!IsIdle())
            {
                throw new ThunderscopeException("SPI is busy");
            }

            if (csIndex >= numCs)
            {
                throw new ThunderscopeException("Requested chip select is out of range");
            }

            // Set Chip Select.
            uint addr = SPI_CS(spiBaseAddress);
            litePci.WriteL(addr, SPI_CS_SEL(csIndex) | SPI_CS_MODE_NORMAL);

            // Prepare MOSI data.
            uint mosiData = (uint)((register << 16) + (data[0] << 8) + data[1]);
            addr = SPI_MOSI(spiBaseAddress);
            litePci.WriteL(addr, mosiData);

            // Start SPI Xfer.
            addr = SPI_CONTROL(spiBaseAddress);
            litePci.WriteL(addr, SPI_CTRL_LENGTH(data.Length + 1) | SPI_CTRL_START);
        }

        public bool IsIdle()
        {
            const uint SPI_STATUS_DONE = (1 << 0);
            return litePci.ReadL(SPI_STATUS(spiBaseAddress)) == SPI_STATUS_DONE;
        }

        public void WaitForIdle(double timeoutSec = 0.1)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            while (!IsIdle())
            {
                if (DateTimeOffset.UtcNow.Subtract(start).TotalSeconds > timeoutSec)
                    throw new Exception("SPI wait for idle exceeded 100ms");
            };
        }

        private uint SPI_CONTROL(uint baseAddress) => ((baseAddress) + 0x00);
        private uint SPI_STATUS(uint baseAddress) => ((baseAddress) + 0x04);
        private uint SPI_MOSI(uint baseAddress) => ((baseAddress) + 0x08);
        private uint SPI_MISO(uint baseAddress) => ((baseAddress) + 0x0C);
        private uint SPI_CS(uint baseAddress) => ((baseAddress) + 0x10);
        private uint SPI_LOOPBACK(uint baseAddress) => ((baseAddress) + 0x14);

        private uint SPI_CS_SEL(uint n) => (uint)(1 << (int)(n));
        private uint SPI_CTRL_LENGTH(int x) => (uint)((8 * (x)) << 8);
    }
}
