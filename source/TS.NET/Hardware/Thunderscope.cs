﻿using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Interop;

namespace TS.NET
{
    public record ThunderscopeDevice(string DevicePath);

    public class Thunderscope
    {
        private ThunderscopeInterop interop;

        private bool open = false;
        private ThunderscopeHardwareState hardwareState = new();

        public ThunderscopeChannel[] Channels = new ThunderscopeChannel[] { new(), new(), new(), new() };

        public static List<ThunderscopeDevice> IterateDevices()
        {
            return ThunderscopeInterop.IterateDevices();
        }

        public void Open(ThunderscopeDevice device)
        {
            if (open)
                Close();

            interop = ThunderscopeInterop.CreateInterop(device);

            Initialise();
            open = true;
        }

        public void Close()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            open = false;
        }

        public void Start()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            if (hardwareState.DatamoverEnabled)
                throw new Exception("Thunderscope already started");
            hardwareState.DatamoverEnabled = true;
            hardwareState.FpgaAdcEnabled = true;
            hardwareState.BufferHead = 0;
            hardwareState.BufferTail = 0;
            ConfigureDatamover(hardwareState);
        }

        public void Stop()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            if (!hardwareState.DatamoverEnabled)
                throw new Exception("Thunderscope not started");
            hardwareState.DatamoverEnabled = false;
            ConfigureDatamover(hardwareState);
            Thread.Sleep(5);
            hardwareState.FpgaAdcEnabled = false;
            ConfigureDatamover(hardwareState);
        }

        public void Read(ThunderscopeMemory data)     //ThunderscopeMemory ensures memory is aligned on 4k boundary
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            if (!hardwareState.DatamoverEnabled)
                throw new ThunderscopeNotRunningException("Thunderscope not started");

            // Buffer data must be aligned to 4096
            //if (0xFFF & (ptrdiff_t)data)
            //{
            //    throw new Exception("data not aligned to 4096 boundary");
            //}

            ulong length = ThunderscopeMemory.Length;
            // Align length to 4096.
            //length &= ~0xFFFUL;

            UpdateBufferHead();

            ulong dataIndex = 0;
            while (length > 0)
            {
                ulong pages_available = hardwareState.BufferHead - hardwareState.BufferTail;
                if (pages_available == 0)
                {

                    //Thread.Sleep(1);
                    //Thread.Yield();
                    Thread.SpinWait(1000);
                    UpdateBufferHead();
                    continue;
                }
                ulong pages_to_read = length >> 12;
                if (pages_to_read > pages_available) pages_to_read = pages_available;
                ulong buffer_read_pos = hardwareState.BufferTail % hardwareState.RamSizePages;
                if (pages_to_read > hardwareState.RamSizePages - buffer_read_pos) pages_to_read = hardwareState.RamSizePages - buffer_read_pos;
                if (pages_to_read > hardwareState.RamSizePages / 4) pages_to_read = hardwareState.RamSizePages / 4;

                interop.ReadC2H(data, dataIndex, buffer_read_pos << 12, pages_to_read << 12);
                //read_handle(ts, ts->c2h0_handle, dataPtr, buffer_read_pos << 12, pages_to_read << 12);

                dataIndex += pages_to_read << 12;
                length -= pages_to_read << 12;

                // Update buffer head and calculate overflow BEFORE
                // updating buffer tail as it is possible
                // that a buffer overflow occured while we were reading.
                UpdateBufferHead();
                hardwareState.BufferTail += pages_to_read;
            }
        }

        private void Initialise()
        {
            Write32(BarRegister.DATAMOVER_REG_OUT, 0);
            
            hardwareState.PllEnabled = true;    //RSTn high --> PLL active
            ConfigureDatamover(hardwareState);
            
            Thread.Sleep(1);
            
            hardwareState.BoardEnabled = true;
            ConfigureDatamover(hardwareState);
            
            ConfigurePLL();
            ConfigureADC();

            hardwareState.FrontEndEnabled = true;
            ConfigureDatamover(hardwareState);

            SetPga(0);
            SetPga(1);
            SetPga(2);
            SetPga(3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint Read32(BarRegister register)
        {
            Span<byte> bytes = new byte[4];
            interop.ReadUser(bytes, (ulong)register);
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }

        private void Write32(BarRegister register, uint value)
        {
            Span<byte> bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            interop.WriteUser(bytes, (ulong)register);
        }

        private void WriteFifo(ReadOnlySpan<byte> data)
        {
            // reset ISR
            Write32(BarRegister.SERIAL_FIFO_ISR_ADDRESS, 0xFFFFFFFFU);
            // read ISR and IER
            Read32(BarRegister.SERIAL_FIFO_ISR_ADDRESS);
            Read32(BarRegister.SERIAL_FIFO_IER_ADDRESS);
            // enable IER
            Write32(BarRegister.SERIAL_FIFO_IER_ADDRESS, 0x0C000000U);
            // Set false TDR
            Write32(BarRegister.SERIAL_FIFO_TDR_ADDRESS, 0x2U);
            // Put data into queue
            for (int i = 0; i < data.Length; i++)
            {
                // TODO: Replace with write32
                interop.WriteUser(data.Slice(i, 1), (ulong)BarRegister.SERIAL_FIFO_DATA_WRITE_REG);
            }
            // read TDFV (vacancy byte)
            Read32(BarRegister.SERIAL_FIFO_TDFV_ADDRESS);
            // write to TLR (the size of the packet)
            Write32(BarRegister.SERIAL_FIFO_TLR_ADDRESS, (uint)(data.Length * 4));
            // read ISR for a done value
            while ((Read32(BarRegister.SERIAL_FIFO_ISR_ADDRESS) >> 24) != 8)
            {
                Thread.Sleep(1);
            }
            // reset ISR
            Write32(BarRegister.SERIAL_FIFO_ISR_ADDRESS, 0xFFFFFFFFU);
            // read TDFV
            Read32(BarRegister.SERIAL_FIFO_TDFV_ADDRESS);
        }

        private void ConfigureDatamover(ThunderscopeHardwareState state)
        {
            uint datamoverRegister = 0;
            if (state.BoardEnabled) datamoverRegister |= 0x01000000;
            if (state.PllEnabled) datamoverRegister |= 0x02000000;
            if (state.FrontEndEnabled) datamoverRegister |= 0x04000000;
            if (state.DatamoverEnabled) datamoverRegister |= 0x1;
            if (state.FpgaAdcEnabled) datamoverRegister |= 0x2;

            int numChannelsEnabled = 0;
            for (int channel = 0; channel < 4; channel++)
            {
                if (Channels[channel].Enabled == true)
                {
                    numChannelsEnabled++;
                }
                if (Channels[channel].Term50Z == true)
                {
                    datamoverRegister |= (uint)1 << (12 + channel);
                }
                if (Channels[channel].VoltsDiv <= 100)
                {
                    datamoverRegister |= (uint)1 << (16 + channel);
                }
                if (Channels[channel].Coupling == ThunderscopeCoupling.DC)
                {
                    datamoverRegister |= (uint)1 << (20 + channel);
                }
            }
            switch (numChannelsEnabled)
            {
                case 0:
                case 1: break; // do nothing
                case 2: datamoverRegister |= 0x10; break;
                default: datamoverRegister |= 0x30; break;
            }
            Write32(BarRegister.DATAMOVER_REG_OUT, datamoverRegister);
        }

        /*
        ------------------------------------------------------------------
        UNCOMMENT BELOW FOR REV 1 BASEBOARD
        ------------------------------------------------------------------

        private void ConfigurePLL()
        {
            // These were provided by the chip configuration tool.
            ushort[] config_clk_gen = {
                0x0010, 0x010B, 0x0233, 0x08B0,
                0x0901, 0x1000, 0x1180, 0x1501,
                0x1600, 0x1705, 0x1900, 0x1A32,
                0x1B00, 0x1C00, 0x1D00, 0x1E00,
                0x1F00, 0x2001, 0x210C, 0x2228,
                0x2303, 0x2408, 0x2500, 0x2600,
                0x2700, 0x2F00, 0x3000, 0x3110,
                0x3200, 0x3300, 0x3400, 0x3500,
                0x3800, 0x4802 };

            // write to the clock generator
            for (int i = 0; i < config_clk_gen.Length / 2; i++)
            {
                SetPllRegister((byte)(config_clk_gen[i] >> 8), (byte)(config_clk_gen[i] & 0xff));
            }

            hardwareState.PllEnabled = true;
            ConfigureDatamover(hardwareState);
        }

        const byte I2C_BYTE_PLL = 0xFF;
        const byte CLOCK_GEN_I2C_ADDRESS_WRITE = 0b10110000;
        private void SetPllRegister(byte register, byte value)
        {
            Span<byte> fifo = new byte[4];
            fifo[0] = I2C_BYTE_PLL;
            fifo[1] = CLOCK_GEN_I2C_ADDRESS_WRITE;
            fifo[2] = register;
            fifo[3] = value;
            WriteFifo(fifo);
        }
       
        ------------------------------------------------------------------
        UNCOMMENT BELOW FOR REV 2&3 BASEBOARDS
        ------------------------------------------------------------------

        private void ConfigurePLL()
        {
            //Strobe RST line on power on
            Thread.Sleep(1);
            hardwareState.PllEnabled = false;    //RSTn low --> PLL reset
            ConfigureDatamover(hardwareState);
            Thread.Sleep(1);
            hardwareState.PllEnabled = true;    //RSTn high --> PLL active
            ConfigureDatamover(hardwareState);
            Thread.Sleep(1);

            // These were provided by the chip configuration tool.
            uint[] config_clk_gen = {
                0X000902, 0X062108, 0X063140, 0X010006,
                0X010120, 0X010202, 0X010380, 0X010A20,
                0X010B03, 0X01140D, 0X012006, 0X0125C0,
                0X012660, 0X01277F, 0X012904, 0X012AB3,
                0X012BC0, 0X012C80, 0X001C10, 0X001D80,
                0X034003, 0X020141, 0X022135, 0X022240,
                0X000C02, 0X000B01};

            // write to the clock generator
            for (int i = 0; i < config_clk_gen.Length; i++)
            {
                SetPllRegister((byte)(config_clk_gen[i] >> 16),(byte)(config_clk_gen[i] >> 8), (byte)(config_clk_gen[i] & 0xff));
            }

            Thread.Sleep(10);

            SetPllRegister((byte)(0x00),(byte)(0x0D), (byte)(0x05));

            Thread.Sleep(10);
        }

        const byte I2C_BYTE_PLL = 0xFF;
        const byte CLOCK_GEN_I2C_ADDRESS_WRITE = 0b11011000;
        const byte CLOCK_GEN_WRITE_COMMAND = 0x02;
        private void SetPllRegister(byte reg_high, byte reg_low, byte value)
        {
            Span<byte> fifo = new byte[6];
            fifo[0] = I2C_BYTE_PLL;
            fifo[1] = CLOCK_GEN_I2C_ADDRESS_WRITE;
            fifo[2] = CLOCK_GEN_WRITE_COMMAND;
            fifo[3] = reg_high;
            fifo[4] = reg_low;
            fifo[5] = value;
            WriteFifo(fifo);
        }

        /*
        ------------------------------------------------------------------
        UNCOMMENT BELOW FOR REV 4 BASEBOARD
        ------------------------------------------------------------------
        */

        private void ConfigurePLL()
        {
            //Strobe RST line on power on
            Thread.Sleep(10);
            hardwareState.PllEnabled = false;    //RSTn low --> PLL reset
            ConfigureDatamover(hardwareState);
            Thread.Sleep(10);
            hardwareState.PllEnabled = true;    //RSTn high --> PLL active
            ConfigureDatamover(hardwareState);
            Thread.Sleep(10);
            
            // These were provided by the chip configuration tool.
            uint[] config_clk_gen = { 
                0x042308, 0x000301, 0x000402, 0x000521,
                0x000701, 0x010042, 0x010100, 0x010201,
                0x010600, 0x010700, 0x010800, 0x010900,
                0x010A20, 0x010B03, 0x012160, 0x012790,
                0x014100, 0x014200, 0x014300, 0x014400,
                0x0145A0, 0x015300, 0x015450, 0x0155CE,
                0x018000, 0x020080, 0x020105, 0x025080,
                0x025102, 0x04300C, 0x043000};

            // write to the clock generator
            for (int i = 0; i < config_clk_gen.Length; i++)
            {
                SetPllRegister((byte)(config_clk_gen[i] >> 16),(byte)(config_clk_gen[i] >> 8), (byte)(config_clk_gen[i] & 0xff));
            }

            Thread.Sleep(10);

            SetPllRegister((byte)(0x01),(byte)(0x00), (byte)(0x02)); //0x010002
            SetPllRegister((byte)(0x01),(byte)(0x00), (byte)(0x42)); //0x010042

            Thread.Sleep(10);
        }

        const byte I2C_BYTE_PLL = 0xFF;
        const byte CLOCK_GEN_I2C_ADDRESS_WRITE = 0b11101000;
        const byte CLOCK_GEN_WRITE_COMMAND = 0x02;
        private void SetPllRegister(byte reg_high, byte reg_low, byte value)
        {
            Span<byte> fifo = new byte[6];
            fifo[0] = I2C_BYTE_PLL;
            fifo[1] = CLOCK_GEN_I2C_ADDRESS_WRITE;
            fifo[2] = CLOCK_GEN_WRITE_COMMAND;
            fifo[3] = reg_high;
            fifo[4] = reg_low;
            fifo[5] = value;
            WriteFifo(fifo);
        }

        private void ConfigureADC()
        {
            // Reset ADC
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_RESET, 0x0001);
            // Power Down ADC
            AdcPower(false);
            // Invert channels
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_INVERT, 0x007F);
            // Adjust full scale value
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_FS_CNTRL, 0x0010);
            // Course Gain On
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_GAIN_CFG, 0x000);
            // Course Gain 4-CH set
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_QUAD_GAIN, 0x9999);
            // Course Gain 1-CH & 2-CH set
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_DUAL_GAIN, 0x0A99);
            //Set adc into active mode
            //currentBoardState.num_ch_on++;
            //currentBoardState.ch_is_on[0] = true;
            //_FIFO_WRITE(user_handle,currentBoardState.adc_chnum_clkdiv,sizeof(currentBoardState.adc_chnum_clkdiv));

            //Select 8-bit mode for HMCAD1520s
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_RES_SEL, 0x0000);

            //Set LVDS phase to 0 Deg & Drive Strength to RSDS
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_LVDS_PHASE, 0x0060);
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_LVDS_DRIVE, 0x0222);

            AdcPower(true);
            //_FIFO_WRITE(user_handle,currentBoardState.adc_in_sel_12,sizeof(currentBoardState.adc_in_sel_12));
            //_FIFO_WRITE(user_handle,currentBoardState.adc_in_sel_34,sizeof(currentBoardState.adc_in_sel_34));

        }

        const byte SPI_BYTE_ADC = 0xFD;
        private void SetAdcRegister(AdcRegister register, ushort value)
        {
            Span<byte> fifo = new byte[4];
            fifo[0] = SPI_BYTE_ADC;
            fifo[1] = (byte)register;
            fifo[2] = (byte)(value >> 8);
            fifo[3] = (byte)(value & 0xff);
            WriteFifo(fifo);
        }

        private void AdcPower(bool on)
        {
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_POWER, (ushort)(on ? 0x0000 : 0x0200));
        }

        public void EnableChannel(int channel)
        {
            Channels[channel].Enabled = true;
            ConfigureChannel(channel);
        }

        public void ResetBuffer()
        {
            hardwareState.BufferHead = 0;
            hardwareState.BufferTail = 0;
            ConfigureDatamover(hardwareState);
        }

        private void ConfigureChannel(int channel)
        {
            ConfigureChannels();
            SetDAC(channel);
            ConfigureDatamover(hardwareState);
            SetPga(channel);
        }

        private void ConfigureChannels()
        {
            byte[] on_channels = new byte[4];
            int num_channels_on = 0;
            byte clkdiv = 0;

            for (int i = 0; i < 4; i++)
            {
                if (Channels[i].Enabled)
                {
                    on_channels[num_channels_on++] = (byte)(3-i);
                }
            }

            switch (num_channels_on)
            {
                case 0:
                case 1:
                    on_channels[0] = on_channels[1] = on_channels[2] = on_channels[3];
                    clkdiv = 0;
                    break;

                case 2:
                    on_channels[0] = on_channels[1];
                    on_channels[2] = on_channels[3];
                    clkdiv = 1;
                    break;

                default:
                    on_channels[0] = 3;
                    on_channels[1] = 2;
                    on_channels[2] = 1;
                    on_channels[3] = 0;
                    num_channels_on = 4;
                    clkdiv = 2;
                    break;
            }

            AdcPower(false);
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_CHNUM_CLKDIV, (ushort)((clkdiv << 8) | num_channels_on));
            AdcPower(true);
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_INSEL12, (ushort)((2 << on_channels[0]) | (512 << on_channels[1])));
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_INSEL34, (ushort)((2 << on_channels[2]) | (512 << on_channels[3])));

            ThunderscopeHardwareState temporaryState = hardwareState;
            temporaryState.DatamoverEnabled = false;
            temporaryState.FpgaAdcEnabled = false;
            ConfigureDatamover(temporaryState);

            Thread.Sleep(5);

            if (num_channels_on != 0)
                ConfigureDatamover(hardwareState);
        }

        private void SetDAC(int channel)
        {
            // value is 12-bit
            // Is this right?? Or is it rounding wrong?
            
            uint[] calVals = {1950,1950,1910,1910};
            
            uint dac_value = (uint)Math.Round((Channels[channel].VoltsOffset * 4095) + calVals[channel]);
            
            
            if (dac_value < 0)
                throw new Exception("DAC offset too low");
            if (dac_value > 0xFFF)
                throw new Exception("DAC offset too high");

            Span<byte> fifo = new byte[5];
            fifo[0] = 0xFF;  // I2C
            fifo[1] = 0xC0;  // DAC? NEW ADDRESS is C0 since part is A0 varient
            fifo[2] = (byte)(0x40 + (channel << 1));
            fifo[3] = (byte)((dac_value >> 8) & 0xF);
            fifo[4] = (byte)(dac_value & 0xFF);
            WriteFifo(fifo);
        }

        private void SetPga(int channel)
        {
            Span<byte> fifo = new byte[4];
            fifo[0] = (byte)(0xFB - channel);  // SPI chip enable
            fifo[1] = 0;
            fifo[2] = 0x04;  // ??

            int vdiv = Channels[channel].VoltsDiv;
            if (vdiv > 100)
            {
                // Attenuator relay on, handled by
                // thunderscopehw_set_datamover_reg.
                vdiv /= 100;
            }

            switch (vdiv)
            {
                case 100: fifo[3] = 0x0A; break;
                case 50: fifo[3] = 0x07; break;
                case 20: fifo[3] = 0x03; break;
                case 10: fifo[3] = 0x1A; break;
                case 5: fifo[3] = 0x17; break;
                case 2: fifo[3] = 0x13; break;
                case 1: fifo[3] = 0x10; break;
                default: throw new Exception("Invalid volts per div");
            }
            switch (Channels[channel].Bandwidth)
            {
                case 20: fifo[3] |= 0x40; break;
                case 100: fifo[3] |= 0x80; break;
                case 200: fifo[3] |= 0xC0; break;
                case 350: /* 0 */ break;
                default: throw new Exception("Invalid bandwidth");
            }
            WriteFifo(fifo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void UpdateBufferHead()
        {
            // 1 page = 4k
            uint transfer_counter = Read32(BarRegister.DATAMOVER_TRANSFER_COUNTER);
            uint error_code = transfer_counter >> 30;
            if ((error_code & 2) > 0)
                throw new Exception("Thunderscope - datamover error");

            if ((error_code & 1) > 0)
                throw new ThunderscopeFIFOOverflowException("Thunderscope - FIFO overflow");

            uint overflow_cycles = (transfer_counter >> 16) & 0x3FFF;
            if (overflow_cycles > 0)
                throw new Exception("Thunderscope - pipeline overflow");

            uint pages_moved = transfer_counter & 0xFFFF;
            ulong buffer_head = (hardwareState.BufferHead & ~0xFFFFUL) | pages_moved;
            if (buffer_head < hardwareState.BufferHead)
                buffer_head += 0x10000UL;

            hardwareState.BufferHead = buffer_head;

            ulong pages_available = hardwareState.BufferHead - hardwareState.BufferTail;
            if (pages_available >= hardwareState.RamSizePages)
                throw new ThunderscopeMemoryOutOfMemoryException("Thunderscope - memory full");
        }
    }
}
