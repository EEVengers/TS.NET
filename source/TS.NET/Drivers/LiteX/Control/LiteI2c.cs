using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX
{
    internal enum LiteI2cClockRate { Rate100kHz = 0, Rate400kHz = 1, Rate1MHz = 2 };

    internal class LiteI2cBus
    {
        private readonly ILogger logger;
        private readonly LitePcie litePci;

        public LiteI2cBus(ILoggerFactory loggerFactory, LitePcie litePci)
        {
            logger = loggerFactory.CreateLogger(nameof(LiteI2cBus));
            this.litePci = litePci ?? throw new ArgumentNullException(nameof(litePci));
        }

        public void SetClockRate(LiteI2cClockRate clockRate)
        {
            Activate(false);
            litePci.WriteL((uint)CSR.CSR_I2C_PHY_SPEED_MODE_ADDR, (uint)clockRate);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>True if the read completed successfully.</returns>
        // TO DO: change this to WriteRead, with ReadOnlySpan<byte> writeData, and Span<byte> readData?
        public bool Read(ushort deviceAddress, uint writeData, byte writeDataSize, Span<byte> readData)
        {
            int i, j;

            if (writeDataSize > 4)
            {
                return false;
            }

            Activate(true);
            SetDeviceAddress(deviceAddress);

            //Write address
            if (writeDataSize > 0)
            {
                if (!Transmit(writeData, writeDataSize))
                {
                    logger.LogError("I2C NACK writing device address 0x{deviceAddress:X2} to register 0x{registerAddress:X}", deviceAddress, writeData);
                    Activate(false);
                    return false;
                }
            }

            // Read Data
            for (i = 0; i < readData.Length; i += 4)
            {
                uint data_word;
                byte rx_size, rx_bytes;
                rx_bytes = rx_size = (byte)(readData.Length - i);
                if (rx_size > 5)
                {
                    rx_size = 5;        // 5 = another transaction is coming, don't stop.
                    rx_bytes = 4;
                }

                if (!Receive(out data_word, rx_size))
                {
                    logger.LogError("I2C Read NACK for address 0x{devAddr:X2}", deviceAddress);
                    Activate(false);
                    return false;
                }

                for (j = 0; j < rx_bytes; j++)
                {
                    readData[i + j] = (byte)((data_word >> (8 * (rx_bytes - 1 - j))) & 0xFF);
                }
            }

            Activate(false);

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>True if the write was ACK'd by the device.</returns>
        public bool Write() { throw new NotImplementedException(); return false; }

        public void Reset() { throw new NotImplementedException(); }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>True if the device is present.</returns>
        public bool Poll() { throw new NotImplementedException(); return false; }

        private void Activate(bool active) => litePci.WriteL((uint)CSR.CSR_I2C_MASTER_ACTIVE_ADDR, (uint)(active ? 1 : 0));
        private void SetDeviceAddress(ushort deviceAddress) => litePci.WriteL((uint)CSR.CSR_I2C_MASTER_ADDR_ADDR, deviceAddress);

        private int I2C_NACK(uint x) => (int)(((x) >> CSR.CSR_I2C_MASTER_STATUS_NACK_OFFSET) & 0x1);
        private uint I2C_TX_LEN(byte x) => (uint)(((x) & 0x7) << CSR.CSR_I2C_MASTER_SETTINGS_LEN_TX_OFFSET);
        private uint I2C_RX_LEN(byte x) => (uint)(((x) & 0x7) << CSR.CSR_I2C_MASTER_SETTINGS_LEN_RX_OFFSET);
        private uint I2C_TX_READY(uint x) => (((x) >> CSR.CSR_I2C_MASTER_STATUS_TX_READY_OFFSET) & 0x1);
        private uint I2C_RX_READY(uint x) => (((x) >> CSR.CSR_I2C_MASTER_STATUS_RX_READY_OFFSET) & 0x1);

        // Transmit 1 to 4 bytes
        private bool Transmit(uint data, byte length)
        {
            bool ack;
            uint i2cStatus;

            //Write Settings
            litePci.WriteL((uint)CSR.CSR_I2C_MASTER_SETTINGS_ADDR, I2C_TX_LEN(length));

            //Wait for TX ready
            WaitForTxReady();

            //Write TX word
            litePci.WriteL((uint)CSR.CSR_I2C_MASTER_RXTX_ADDR, data);

            //Wait for RX ready
            WaitForRxReady();

            //Get ACK
            i2cStatus = litePci.ReadL((uint)CSR.CSR_I2C_MASTER_STATUS_ADDR);
            ack = I2C_NACK(i2cStatus) > 0 ? false : true;

            //Read the RX word
            i2cStatus = litePci.ReadL((uint)CSR.CSR_I2C_MASTER_RXTX_ADDR);

            return ack;
        }

        // Receive 1 to 4 bytes
        private bool Receive(out uint data, byte length)
        {
            uint status = 0;
            bool ack;

            //Write Settings
            litePci.WriteL((uint)CSR.CSR_I2C_MASTER_SETTINGS_ADDR, I2C_RX_LEN(length));

            //Wait for TX ready
            WaitForTxReady();

            //Write TX word
            litePci.WriteL((uint)CSR.CSR_I2C_MASTER_RXTX_ADDR, 0);

            //Wait for RX Ready
            WaitForRxReady();

            //Get ACK
            status = litePci.ReadL((uint)CSR.CSR_I2C_MASTER_STATUS_ADDR);
            ack = I2C_NACK(status) > 0 ? false : true;

            //Read Data word
            data = litePci.ReadL((uint)CSR.CSR_I2C_MASTER_RXTX_ADDR);

            return ack;
        }

        private void WaitForTxReady(double timeoutSec = 0.1)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            while (I2C_TX_READY(litePci.ReadL((uint)CSR.CSR_I2C_MASTER_STATUS_ADDR)) == 0)
            {
                Thread.Sleep(1);
                if (DateTimeOffset.UtcNow.Subtract(start).TotalSeconds > timeoutSec)
                    break;
            }
        }

        private void WaitForRxReady(double timeoutSec = 0.1)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            while (I2C_RX_READY(litePci.ReadL((uint)CSR.CSR_I2C_MASTER_STATUS_ADDR)) == 0)
            {
                Thread.Sleep(1);
                if (DateTimeOffset.UtcNow.Subtract(start).TotalSeconds > timeoutSec)
                    break;
            }
        }
    }

    internal class LiteI2cDevice
    {
        private readonly LiteI2cBus bus;
        private readonly ushort address;

        public LiteI2cDevice(LiteI2cBus bus, ushort address)
        {
            this.bus = bus;
        }
    }
}
