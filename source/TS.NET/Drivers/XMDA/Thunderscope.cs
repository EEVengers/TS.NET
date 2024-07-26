using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using TS.NET.Driver.XMDA.Interop;

namespace TS.NET.Driver.XMDA
{
    public record ThunderscopeDevice(string DevicePath);

    public class Thunderscope : IThunderscope
    {
        private readonly ILogger logger;
        private ThunderscopeInterop interop;
        private ThunderscopeCalibration calibration;
        private bool open = false;
        private ThunderscopeHardwareState hardwareState;
        private ThunderscopeHardwareConfig configuration;
        private string revision;

        public Thunderscope(ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger(nameof(ThunderscopeDevice));
            hardwareState = new();
            configuration = new()
            {
                AdcChannelMode = AdcChannelMode.Quad,
                EnabledChannels = 0b00001111
            };
            configuration.Channels[0] = ThunderscopeChannel.Default();
            configuration.Channels[1] = ThunderscopeChannel.Default();
            configuration.Channels[2] = ThunderscopeChannel.Default();
            configuration.Channels[3] = ThunderscopeChannel.Default();
        }

        public static List<ThunderscopeDevice> IterateDevices()
        {
            return ThunderscopeInterop.IterateDevices();
        }

        public void Open(ThunderscopeDevice device, ThunderscopeCalibration calibration, string revision)
        {
            if (open)
                Close();

            interop = ThunderscopeInterop.CreateInterop(device);
            this.calibration = calibration;
            this.revision = revision;

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

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)     //ThunderscopeMemory ensures memory is aligned on 4k boundary
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

        // Returns a by-value copy
        public ThunderscopeChannel GetChannel(int channelIndex)
        {
            return configuration.Channels[channelIndex];
        }

        public void SetChannel(int channel, ThunderscopeChannel thunderscopeChannel)
        {
            UpdateAdc();
            UpdateAfe(channel, ref thunderscopeChannel);
            configuration.Channels[channel] = thunderscopeChannel;
        }

        public void SetChannelEnable(int channelIndex, bool enabled)
        {
            if (enabled)
            {
                configuration.EnabledChannels |= (byte)(0x01 << channelIndex);
            }
            else
            {
                configuration.EnabledChannels &= (byte)~(0x01 << channelIndex);
            }
        }

        // Returns a by-value copy
        public ThunderscopeHardwareConfig GetConfiguration()
        {
            return configuration;
        }

        public void ResetBuffer()
        {
            hardwareState.BufferHead = 0;
            hardwareState.BufferTail = 0;
            ConfigureDatamover(hardwareState);
        }

        private void Initialise()
        {
            Write32(BarRegister.DATAMOVER_REG_OUT, 0);

            //Comment out below for Rev.1
            hardwareState.PllEnabled = true;    //RSTn high --> PLL active
            ConfigureDatamover(hardwareState);
            Thread.Sleep(1);
            //Comment out above for Rev.1

            hardwareState.BoardEnabled = true;
            ConfigureDatamover(hardwareState);
            ConfigurePLL();
            ConfigureAdc();

            UpdateAdc();

            for (int channel = 0; channel < 4; channel++)
            {
                UpdateAfe(channel, ref configuration.Channels[channel]);
            }

            ConfigureDatamover(hardwareState);      // Needed to set Attenuator after UpdateAfe
        }

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
            // Put data into queue
            for (int i = 0; i < data.Length; i++)
            {
                //interop.WriteUser(data.Slice(i, 1), (ulong)BarRegister.SERIAL_FIFO_DATA_WRITE_REG);
                Span<byte> bytes = new byte[4];
                bytes.Fill(data[i]);
                interop.WriteUser(bytes, (ulong)BarRegister.SERIAL_FIFO_DATA_WRITE_REG);
            }
            // write to TLR (the size of the packet)
            Write32(BarRegister.SERIAL_FIFO_TLR_ADDRESS, (uint)(data.Length * 4));
            // read ISR for a done value
            while (Read32(BarRegister.SERIAL_FIFO_ISR_ADDRESS) >> 24 != 8)
            {
                Thread.Sleep(1);
            }
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
                if (((configuration.EnabledChannels >> channel) & 0x01) > 0)
                {
                    numChannelsEnabled++;
                }
                if (configuration.Channels[channel].Termination == ThunderscopeTermination.FiftyOhm)
                {
                    datamoverRegister |= (uint)1 << (12 + channel);
                }
                if (!configuration.Channels[channel].Attenuator)
                {
                    datamoverRegister |= (uint)1 << 16 + channel;
                }
                if (configuration.Channels[channel].Coupling == ThunderscopeCoupling.DC)
                {
                    datamoverRegister |= (uint)1 << 20 + channel;
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

        private void ConfigureAdc()
        {
            // Reset ADC
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_RESET, 0x0001);
            // Power Down ADC
            AdcPower(false);
            // Invert channels
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_INVERT, 0x007F);
            // Adjust full scale value
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_FS_CNTRL, 0x0020);
            // Course Gain On
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_GAIN_CFG, 0x0000);
            // Course Gain 4-CH set
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_QUAD_GAIN, 0x9999);
            // Course Gain 1-CH & 2-CH set
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_DUAL_GAIN, 0x0A99);
            //Set adc into active mode
            //currentBoardState.num_ch_on++;
            //currentBoardState.ch_is_on[0] = true;
            //_FIFO_WRITE(user_handle,currentBoardState.adc_chnum_clkdiv,sizeof(currentBoardState.adc_chnum_clkdiv));

            // Set 8-bit mode (for HMCAD1520, won't do anything for HMCAD1511)
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_RES_SEL, 0x0000);

            //Set LVDS phase to 0 Deg & Drive Strength to RSDS
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_LVDS_PHASE, 0x0060);
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_LVDS_DRIVE, 0x0222);

            AdcPower(true);
            //_FIFO_WRITE(user_handle,currentBoardState.adc_in_sel_12,sizeof(currentBoardState.adc_in_sel_12));
            //_FIFO_WRITE(user_handle,currentBoardState.adc_in_sel_34,sizeof(currentBoardState.adc_in_sel_34));

            hardwareState.FrontEndEnabled = true;
            ConfigureDatamover(hardwareState);
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

        private void UpdateAdc()
        {
            byte[] insel = new byte[4];
            int num_channels_on = 0;
            for (int i = 0; i < 4; i++)
            {
                if (((configuration.EnabledChannels >> i) & 0x01) > 0)
                {
                    num_channels_on++;
                }
            }

            byte clkdiv;
            switch (num_channels_on)
            {
                case 0:
                case 1:
                    for (byte i = 3; i >= 0; i--){
                        if (((configuration.EnabledChannels >> i) & 0x01) > 0){
                            insel[3] = insel[2] = insel[1] = insel[0] = (byte)(3-i);
                            break;
                        }
                    }
                    clkdiv = 0;
                    configuration.AdcChannelMode = AdcChannelMode.Single;
                    break;
                case 2:
                    for (byte i = 3; i >= 0; i--){
                        if (((configuration.EnabledChannels >> i) & 0x01) > 0){
                            insel[3] = insel[2] = (byte)(3-i);
                            break;
                        }
                    }                
                    for (byte i = 0; i < 4; i++){
                        if (((configuration.EnabledChannels >> i) & 0x01) > 0){
                            insel[1] = insel[0] = (byte)(3-i);
                            break;
                        }
                    }
                    clkdiv = 1;
                    configuration.AdcChannelMode = AdcChannelMode.Dual;
                    break;
                default:
                    insel[0] = 3;
                    insel[1] = 2;
                    insel[2] = 1;
                    insel[3] = 0;
                    num_channels_on = 4;
                    clkdiv = 2;
                    configuration.AdcChannelMode = AdcChannelMode.Quad;
                    break;
            }

            AdcPower(false);
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_CHNUM_CLKDIV, (ushort)(clkdiv << 8 | num_channels_on));
            AdcPower(true);
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_INSEL12, (ushort)(2 << insel[0] | 512 << insel[1]));
            SetAdcRegister(AdcRegister.THUNDERSCOPEHW_ADC_REG_INSEL34, (ushort)(2 << insel[2] | 512 << insel[3]));

            ThunderscopeHardwareState temporaryState = hardwareState;
            temporaryState.DatamoverEnabled = false;
            temporaryState.FpgaAdcEnabled = false;
            ConfigureDatamover(temporaryState);

            Thread.Sleep(5);

            if (num_channels_on != 0)
                ConfigureDatamover(hardwareState);
        }

        private void UpdateAfe(int channel, ref ThunderscopeChannel channelConfiguration)
        {
            ThunderscopeChannelCalibration channelCal = channel switch
            {
                0 => calibration.Channel1,
                1 => calibration.Channel2,
                2 => calibration.Channel3,
                3 => calibration.Channel4,
            };

            CalculatePgaConfigurationWord(ref channelCal, ref channelConfiguration);
            SetPGA(channel, channelConfiguration.PgaConfigurationWord);
            Thread.Sleep(1);

            CalculateTrimConfiguration(ref channelCal, ref channelConfiguration);

            // To do: use the calibration to calculate actual value
            ushort dacValue = channelConfiguration.TrimOffsetDac;
            SetTrimOffsetDAC(channel, dacValue);
            Thread.Sleep(1);

            // To do: use the calibration to calculate actual value
            dacValue = channelConfiguration.TrimSensitivityDac;
            SetTrimSensitivityDAC(channel, dacValue);
            Thread.Sleep(1);
        }

        private void SetTrimOffsetDAC(int channel, ushort dacValue)
        {
            // MCP4728 - 12-bit quad DAC with EEPROM
            if (dacValue < 0)
                throw new Exception("DAC value too low");
            if (dacValue > 0xFFF)
                throw new Exception("DAC value too high");

            Span<byte> fifo = new byte[5];
            fifo[0] = 0xFF;                             // I2C
            fifo[1] = 0xC0;                             // MCP4728 8-bit address
            fifo[2] = (byte)(0x40 + (channel << 1));    // p34 of MCP4728 datasheet
            fifo[3] = (byte)(dacValue >> 8 & 0xF);      // Vref = VDD. Power down = normal mode. Gain = 1
            fifo[4] = (byte)(dacValue & 0xFF);
            WriteFifo(fifo);
        }

        private void SetTrimSensitivityDAC(int channel, ushort dacValue)
        {
            // MCP4432 - 7-bit quad Digital POT
            if (dacValue < 0)
                throw new Exception("DAC value too low");
            if (dacValue > 0x80)
                throw new Exception("DAC value too high");

            byte command = channel switch
            {
                0 => 0x06 << 4,
                1 => 0x00 << 4,
                2 => 0x01 << 4,
                3 => 0x07 << 4
            };

            Span<byte> fifo = new byte[5];
            fifo[0] = 0xFF;             // I2C
            fifo[1] = 0x58;             // MCP4432 8-bit address
            fifo[2] = command;          // p41 of MCP4432 datasheet
            fifo[3] = (byte)dacValue;   // 8-bit value (0x00 to 0x80 for 7-bit DAC)
            WriteFifo(fifo);
        }

        private void SetPGA(int channel, ushort configurationWord)
        {
            Span<byte> fifo = new byte[4];
            fifo[0] = (byte)(0xFB - channel);           // SPI chip enable
            fifo[1] = 0;
            fifo[2] = (byte)(configurationWord << 8);
            fifo[3] = (byte)(configurationWord & 0xFF);
            WriteFifo(fifo);
        }

        private void UpdateBufferHead()
        {
            // 1 page = 4k
            uint transfer_counter = Read32(BarRegister.DATAMOVER_TRANSFER_COUNTER);
            uint error_code = transfer_counter >> 30;
            if ((error_code & 2) > 0)
                throw new Exception("Thunderscope - datamover error");

            if ((error_code & 1) > 0)
                throw new ThunderscopeFifoOverflowException("Thunderscope - FIFO overflow");

            uint overflow_cycles = transfer_counter >> 16 & 0x3FFF;
            if (overflow_cycles > 0)
                throw new Exception("Thunderscope - pipeline overflow");

            uint pages_moved = transfer_counter & 0xFFFF;
            ulong buffer_head = hardwareState.BufferHead & ~0xFFFFUL | pages_moved;
            if (buffer_head < hardwareState.BufferHead)
                buffer_head += 0x10000UL;

            hardwareState.BufferHead = buffer_head;

            ulong pages_available = hardwareState.BufferHead - hardwareState.BufferTail;
            if (pages_available >= hardwareState.RamSizePages)
                throw new ThunderscopeMemoryOutOfMemoryException("Thunderscope - memory full");
        }

        private void CalculatePgaConfigurationWord(ref ThunderscopeChannelCalibration channelCal, ref ThunderscopeChannel channel)
        {
            // ALL GAINS NOW IN dB

            // This encapsulates ADC gain, TODO: break it out into true full scale range and gain later (both controllable via ADC SPI)
            double adcFullScaleRange = 2;
            double adcGain = 9;

            // Calculate System Gain that would bring the user requested full scale voltage to the adc equivalent full scale voltage
            double requestedSystemGain = 20 * Math.Log10(adcFullScaleRange / channel.VoltFullScale);

            //logger.LogTrace($"Requested FS Voltage: {channel.VoltFullScale:F3}");
            //logger.LogTrace($"Requested System Gain dB: {requestedSystemGain:F3}");
            
            // We can't avoid adding these gains
            double fixedGain = channelCal.BufferGain + channelCal.PgaOutputAmpGain + adcGain; //Putting ADC Gain as fixed for now
            // If we are in 50 Ohm mode we have to add the attenuator gain of the 50 ohm terminator (40 Ohm - 10 Ohm divider)
            if (channel.Termination == ThunderscopeTermination.FiftyOhm)
                fixedGain += channelCal.AttenuatorGainFiftyOhm;
            // What's left are the gains we can control - PGA and 1M Attenuator (and we can't use the 1M Attenuator in 50 Ohm mode)
            double requestedVariableGain = requestedSystemGain - fixedGain;
            
            // This is the lowest variable gain we can do without the 1M Attenuator
            double pgaMinVariableGain = channelCal.PgaPreampLowGain + channelCal.PgaAttenuatorGain10;
            // Keep the 1M Attenuator off unless we need it, or if we can't use it (50 Ohm mode)
            channel.Attenuator = false;
            if ((requestedVariableGain < pgaMinVariableGain) && (channel.Termination == ThunderscopeTermination.OneMegaohm)){
                channel.Attenuator = true;
                requestedVariableGain -= channelCal.AttenuatorGainHighZ;
            }
            
            // Now check all the PGA gain options, starting from highest gain setting
            bool gainFound = false;
            bool preamp = true;
            double ladderGain = channelCal.PgaAttenuatorGain0;
            byte ladderSetting = 0;
            double potentialPgaVariableGain = channelCal.PgaPreampLowGain + ladderGain;

            for (int x = 0; x < 11; x++)
            {
                ladderGain = x switch
                {
                    0 => channelCal.PgaAttenuatorGain0,
                    1 => channelCal.PgaAttenuatorGain1,
                    2 => channelCal.PgaAttenuatorGain2,
                    3 => channelCal.PgaAttenuatorGain3,
                    4 => channelCal.PgaAttenuatorGain4,
                    5 => channelCal.PgaAttenuatorGain5,
                    6 => channelCal.PgaAttenuatorGain6,
                    7 => channelCal.PgaAttenuatorGain7,
                    8 => channelCal.PgaAttenuatorGain8,
                    9 => channelCal.PgaAttenuatorGain9,
                    10 => channelCal.PgaAttenuatorGain10,
                };

                potentialPgaVariableGain = (preamp ? channelCal.PgaPreampHighGain : channelCal.PgaPreampLowGain) + ladderGain;

                if (potentialPgaVariableGain < requestedVariableGain)
                {
                    gainFound = true;
                    channel.PgaIsHighGain = true;
                    ladderSetting = (byte)x;
                    break;
                }
            }
            if (!gainFound)
            {
                preamp = false;
                for (int x = 0; x < 11; x++)
                {
                    ladderGain = x switch
                    {
                        0 => channelCal.PgaAttenuatorGain0,
                        1 => channelCal.PgaAttenuatorGain1,
                        2 => channelCal.PgaAttenuatorGain2,
                        3 => channelCal.PgaAttenuatorGain3,
                        4 => channelCal.PgaAttenuatorGain4,
                        5 => channelCal.PgaAttenuatorGain5,
                        6 => channelCal.PgaAttenuatorGain6,
                        7 => channelCal.PgaAttenuatorGain7,
                        8 => channelCal.PgaAttenuatorGain8,
                        9 => channelCal.PgaAttenuatorGain9,
                        10 => channelCal.PgaAttenuatorGain10,
                    };

                    potentialPgaVariableGain = (preamp ? channelCal.PgaPreampHighGain : channelCal.PgaPreampLowGain) + ladderGain;
                    if (potentialPgaVariableGain < requestedVariableGain)
                    {
                        gainFound = true;
                        channel.PgaIsHighGain = false;
                        ladderSetting = (byte)x;
                        break;
                    }
                }
            }
            if (!gainFound)
                throw new NotSupportedException();

            // Calculate actual system gain with chosen variable gain value
            double actualSystemGainDb = potentialPgaVariableGain;
            // Add 1M attenuator if it was enabled earlier
            if (channel.Attenuator)
                actualSystemGainDb += channelCal.AttenuatorGainHighZ;
            // Add fixed gain value from earlier (includes 50 Ohm attenuator if in 50 Ohm mode)
            actualSystemGainDb += fixedGain;

            // Calculate actual full scale voltage from actual gain
            channel.ActualVoltFullScale = adcFullScaleRange / Math.Pow(10, actualSystemGainDb / 20);

            //logger.LogTrace($"System gain: {actualSystemGainDb:F3}dB ");
            logger.LogTrace($"System gain to PGA: {Math.Pow(10, (actualSystemGainDb) / 20):F3} V/V ");
            logger.LogTrace($"ActualVoltFullScale: {channel.ActualVoltFullScale:F3}V ");

            // Decode N into PGA LNA gain and PGA attentuator step
            channel.PgaConfigurationWord = (byte)ladderSetting;
            if (preamp)
                channel.PgaConfigurationWord |= 0x10;

            channel.PgaConfigurationWord |= 0x400;  // Aux Hi-Z

            switch (channel.Bandwidth)
            {
                case ThunderscopeBandwidth.BwFull: 
                    break;
                case ThunderscopeBandwidth.Bw20M:
                    channel.PgaConfigurationWord |= 1 << 6; 
                    break;
                case ThunderscopeBandwidth.Bw100M:
                    channel.PgaConfigurationWord |= 2 << 6; 
                    break;
                case ThunderscopeBandwidth.Bw200M:
                    channel.PgaConfigurationWord |= 3 << 6; 
                    break;
                case ThunderscopeBandwidth.Bw350M:
                    channel.PgaConfigurationWord |= 4 << 6;
                    break;
                case ThunderscopeBandwidth.Bw650M:
                    channel.PgaConfigurationWord |= 5 << 6;
                    break;
                case ThunderscopeBandwidth.Bw750M:
                    channel.PgaConfigurationWord |= 6 << 6;
                    break;
                default: 
                    throw new Exception("ThunderscopeBandwidth enum value not handled");
            }
        }
        //Works on really low voltage ranges now
        private void CalculateTrimConfiguration(ref ThunderscopeChannelCalibration channelCal, ref ThunderscopeChannel channel)
        {         
            // This encapsulates ADC gain, TODO: break it out into true full scale range and gain later (both controllable via ADC SPI)
            //double adcEquivFullScaleRange = 0.7;             
            //Calulate the requested offset at the PGA_N terminal
            double systemGainToPga = channelCal.BufferGain;
            if (channel.Termination == ThunderscopeTermination.FiftyOhm)
                systemGainToPga += channelCal.AttenuatorGainFiftyOhm;
            else if (channel.Attenuator)
                systemGainToPga += channelCal.AttenuatorGainHighZ;

            double requestedOffsetVoltageAtPGA = channel.VoltOffset/Math.Pow(10, systemGainToPga / 20);

            logger.LogTrace($"Requested Offset: {requestedOffsetVoltageAtPGA:F3}");

            //Decode cal vals from codes to voltage at the PGA_N terminal
            double calibratedVoltageAtPgaNeg = channelCal.HardwareOffsetVoltageLowGain;
            if (channel.PgaIsHighGain)
                 calibratedVoltageAtPgaNeg = channelCal.HardwareOffsetVoltageHighGain;
            
            //Add requested offset to our hardware offset calibrated "zero"
            double requestedVoltageAtPgaNeg = calibratedVoltageAtPgaNeg + requestedOffsetVoltageAtPGA;

            logger.LogTrace($"Requested Voltage: {requestedVoltageAtPgaNeg:F3}");

            //Figure out what VDAC to use, start by keeping VDAC at maximum or minimum
            ushort digipotCode;
            ushort dacCode;
            double VDAC;
            double RTRIM;
            double requestedRTRIM;

            //Figure out what RTRIM to use, start by keeping VDAC at maximum or minimum
            if (requestedVoltageAtPgaNeg > 2.5){
                VDAC = 4095 * (5/4096);
                requestedRTRIM = -(1000*(requestedVoltageAtPgaNeg-5))/(2*requestedVoltageAtPgaNeg-5);
            }
            else{
                VDAC = 0;
                requestedRTRIM = (1000*requestedVoltageAtPgaNeg)/(5-2*requestedVoltageAtPgaNeg);
            }
            
            //Set digipot code based on above
            if (requestedRTRIM > 50000){
                digipotCode = 128; //Can't go higher, recalc VDAC with max RTRIM
            }
            else if (requestedRTRIM < 75){
                logger.LogTrace($"OFFSET OUT OF BOUNDS - RTRIM"); //We won't be able to do this offset
                digipotCode = 0;
            }
            else{
                digipotCode = (ushort)(requestedRTRIM / 50000 * 128); //rounding down is intentional
            }

            //logger.LogTrace($"Calculated Digipot Code: {digipotCode:F3}");
            RTRIM = digipotCode * (50000/128) + 75;
            //logger.LogTrace($"Calculated RTRIM: {RTRIM:F3}");

            VDAC = (RTRIM*(2*requestedVoltageAtPgaNeg-5)/1000) + requestedVoltageAtPgaNeg;
            //logger.LogTrace($"Calculated VDAC: {VDAC:F3}");

            if (VDAC > 4.998778286875){
                logger.LogTrace($"OFFSET OUT OF BOUNDS - VDAC HIGH"); //We won't be able to do this offset
                dacCode = 4095;
            }
            else if (VDAC < 0){
                logger.LogTrace($"OFFSET OUT OF BOUNDS - VDAC LOW"); //We won't be able to do this offset
                dacCode = 0;
            }
            else{
                dacCode = (ushort)(VDAC / 5 * 4096);
            }

            channel.TrimSensitivityDac = digipotCode;
            channel.TrimOffsetDac = dacCode;
            
        }

        private void ConfigurePLLRev1()
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
                SetPllRegisterRev1((byte)(config_clk_gen[i] >> 8), (byte)(config_clk_gen[i] & 0xff));
            }

            hardwareState.PllEnabled = true;
            ConfigureDatamover(hardwareState);
        }

        const byte I2C_BYTE_PLL_Rev1 = 0xFF;
        const byte CLOCK_GEN_I2C_ADDRESS_WRITE_Rev1 = 0b10110000;
        private void SetPllRegisterRev1(byte register, byte value)
        {
            Span<byte> fifo = new byte[4];
            fifo[0] = I2C_BYTE_PLL_Rev1;
            fifo[1] = CLOCK_GEN_I2C_ADDRESS_WRITE_Rev1;
            fifo[2] = register;
            fifo[3] = value;
            WriteFifo(fifo);
        }

        private void ConfigurePLLRev3()
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
                SetPllRegisterRev3((byte)(config_clk_gen[i] >> 16), (byte)(config_clk_gen[i] >> 8), (byte)(config_clk_gen[i] & 0xff));
            }

            Thread.Sleep(10);

            SetPllRegisterRev3(0x00, 0x0D, 0x05);

            Thread.Sleep(10);
        }

        const byte I2C_BYTE_PLL_Rev3 = 0xFF;
        const byte CLOCK_GEN_I2C_ADDRESS_WRITE_Rev3 = 0b11011000;
        const byte CLOCK_GEN_WRITE_COMMAND_Rev3 = 0x02;
        private void SetPllRegisterRev3(byte reg_high, byte reg_low, byte value)
        {
            Span<byte> fifo = new byte[6];
            fifo[0] = I2C_BYTE_PLL_Rev3;
            fifo[1] = CLOCK_GEN_I2C_ADDRESS_WRITE_Rev3;
            fifo[2] = CLOCK_GEN_WRITE_COMMAND_Rev3;
            fifo[3] = reg_high;
            fifo[4] = reg_low;
            fifo[5] = value;
            WriteFifo(fifo);
        }

        private void ConfigurePLLRev4()
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
                SetPllRegisterRev4((byte)(config_clk_gen[i] >> 16), (byte)(config_clk_gen[i] >> 8), (byte)(config_clk_gen[i] & 0xff));
            }

            Thread.Sleep(10);

            SetPllRegisterRev4((byte)(0x01), (byte)(0x00), (byte)(0x02)); //0x010002
            SetPllRegisterRev4((byte)(0x01), (byte)(0x00), (byte)(0x42)); //0x010042

            Thread.Sleep(10);
        }

        const byte I2C_BYTE_PLL_Rev4 = 0xFF;
        const byte CLOCK_GEN_I2C_ADDRESS_WRITE_Rev4 = 0b11101000;
        const byte CLOCK_GEN_WRITE_COMMAND_Rev4 = 0x02;
        private void SetPllRegisterRev4(byte reg_high, byte reg_low, byte value)
        {
            Span<byte> fifo = new byte[6];
            fifo[0] = I2C_BYTE_PLL_Rev4;
            fifo[1] = CLOCK_GEN_I2C_ADDRESS_WRITE_Rev4;
            fifo[2] = CLOCK_GEN_WRITE_COMMAND_Rev4;
            fifo[3] = reg_high;
            fifo[4] = reg_low;
            fifo[5] = value;
            WriteFifo(fifo);
        }

        private void ConfigurePLL()
        {
            switch (revision.ToLower())
            {
                case "rev1":
                    ConfigurePLLRev1();
                    break;
                case "rev3":
                    ConfigurePLLRev3();
                    break;
                case "rev4":
                    ConfigurePLLRev4();
                    break;
                default:
                    throw new ArgumentNullException("No revision set for ThunderScope");

            }
        }
    }
}