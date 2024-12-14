using TS.NET.Driver.LiteX;

namespace TS.NET.Drivers.LiteX
{
    internal class LiteSpiBus
    {
        public LitePcie litePci;
        public uint spiBase;
        public uint numCs;
        public uint csMask;

        public LiteSpiBus(LitePcie litePci, uint spiBase, uint numCs)
        {
            uint SPI_CS_SEL_MASK(uint n) { return (uint)((1 << (int)(n)) - 1); }

            this.litePci = litePci;
            this.spiBase = spiBase;
            this.numCs = numCs;
            this.csMask = SPI_CS_SEL_MASK(numCs);
        }
    }

    internal class LiteSpiDevice
    {
        uint SPI_CONTROL(uint baseAddress) => ((baseAddress) + 0x00);
        uint SPI_STATUS(uint baseAddress) => ((baseAddress) + 0x04);
        uint SPI_MOSI(uint baseAddress) => ((baseAddress) + 0x08);
        uint SPI_MISO(uint baseAddress) => ((baseAddress) + 0x0C);
        uint SPI_CS(uint baseAddress) => ((baseAddress) + 0x10);
        uint SPI_LOOPBACK(uint baseAddress) => ((baseAddress) + 0x14);

        uint SPI_CS_SEL(uint n) => (uint)(1 << (int)(n));
        uint SPI_CTRL_LENGTH(int x) => (uint)((8 * (x)) << 8);

        const uint SPI_CS_MODE_NORMAL = 0;
        const uint SPI_CTRL_START = (1 << 0);

        private LiteSpiBus bus;
        private uint csIndex;

        public LiteSpiDevice(LiteSpiBus bus, uint csIndex)
        {
            this.bus = bus;
            this.csIndex = csIndex;
        }

        public void Write(byte register, ReadOnlySpan<byte> data)
        {
            if (!IsIdle())
            {
                throw new Exception("SPI is busy");
            }

            // Set Chip Select.
            uint addr = SPI_CS(bus.spiBase);
            bus.litePci.WriteL(addr, SPI_CS_SEL(csIndex) | SPI_CS_MODE_NORMAL);

            // Prepare MOSI data.
            uint mosi_data = (uint)((register << 16) + (data[0] << 8) + data[1]);

            addr = SPI_MOSI(bus.spiBase);
            bus.litePci.WriteL(addr, mosi_data);

            // Start SPI Xfer.
            addr = SPI_CONTROL(bus.spiBase);
            bus.litePci.WriteL(addr, SPI_CTRL_LENGTH(data.Length + 1) | SPI_CTRL_START);
        }

        public bool IsIdle()
        {
            const uint SPI_STATUS_DONE = (1 << 0);
            return bus.litePci.ReadL(SPI_STATUS(bus.spiBase)) == SPI_STATUS_DONE;
        }

        public void WaitForIdle()
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            while (!IsIdle())
            {
                if (DateTimeOffset.UtcNow.Subtract(start).Seconds > 0.1)
                    throw new Exception("SPI wait for idle exceeded 100ms");
            };
        }
    }
}
