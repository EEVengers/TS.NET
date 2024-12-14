using Microsoft.Extensions.Logging;
using TS.NET.Hardware;

namespace TS.NET.Drivers.LiteX
{
    internal enum Hmcad15xxPowerMode { Active = 0, Sleep = 1, PowerDown = 2 }
    internal record struct Hmcad15xxChannelConfiguration(bool Active, byte Input, bool Coarse, bool Fine, bool Invert);

    internal class Hmcad15xx
    {
        private readonly ILogger logger;
        private readonly LiteSpiDevice spiDevice;

        private Hmcad15xxLvdsOutputDriveStrength lvdsDrive = Hmcad15xxLvdsOutputDriveStrength.Strength1_5mA;
        private Hmcad15xxLvdsPhase lvdsPhase = Hmcad15xxLvdsPhase.Phase0Deg;
        private Hmcad15xxLvdsDataWidth lvdsDataWidth = Hmcad15xxLvdsDataWidth.Width8Bit;
        private Hmcad15xxLvdsLowClockFrequency lowClockFrequency = Hmcad15xxLvdsLowClockFrequency.Inactive;

        private Hmcad15xxDataBtcFormat dataBtcFormat = Hmcad15xxDataBtcFormat.TwosComplement;
        private Hmcad15xxMode mode = Hmcad15xxMode.SingleChannel;
        private Hmcad15xxClockDiv clockDiv = Hmcad15xxClockDiv.Div1;

        private Hmcad15xxChannelConfiguration[] channelConfiguration =
        [
            new Hmcad15xxChannelConfiguration(Active: true, Input: 1, Coarse: false, Fine: false, Invert: true),
            new Hmcad15xxChannelConfiguration(Active: false, Input: 2, Coarse: false, Fine: false, Invert: true),
            new Hmcad15xxChannelConfiguration(Active: false, Input: 4, Coarse: false, Fine: false, Invert: true),
            new Hmcad15xxChannelConfiguration(Active: false, Input: 8, Coarse: false, Fine: false, Invert: true)
        ];

        private sbyte fullScaleAdjust = 0;

        public Hmcad15xx(ILoggerFactory loggerFactory, LiteSpiDevice spiDevice)
        {
            logger = loggerFactory.CreateLogger(nameof(Hmcad15xx));
            this.spiDevice = spiDevice;
        }

        /// <summary>
        /// Initialise & perform a soft reset, placing the chip into a sleep state.
        /// </summary>
        public void Init()
        {
            //adc->fullScale_x10 = HMCAD15_FULL_SCALE_DEFAULT;

            Reset();
            PowerMode(Hmcad15xxPowerMode.PowerDown);
            ApplyLvdsMode();

            // Gain dB mode
            RegisterWrite(Hmcad15xxRegister.GAIN_SEL, 0);

            // Channel Conf
            ApplySampleMode();
            ApplyChannelMap();
            ApplyChannelGain();

            PowerMode(Hmcad15xxPowerMode.Sleep);
        }

        /// <summary>
        /// Perform a soft reset.
        /// </summary>
        public void Reset()
        {
            RegisterWrite(Hmcad15xxRegister.SW_RST, 1);
        }

        /// <summary>
        /// Set the power mode.
        /// </summary>
        public void PowerMode(Hmcad15xxPowerMode powerMode)
        {
            ushort data = 0;

            const ushort HMCAD15_SINGLE_CH_SLP = (1 << 6);
            const ushort HMCAD15_DUAL_CH_1_SLP = (1 << 5);
            const ushort HMCAD15_DUAL_CH_0_SLP = (1 << 4);
            const ushort HMCAD15_QUAD_CH_3_SLP = (1 << 3);
            const ushort HMCAD15_QUAD_CH_2_SLP = (1 << 2);
            const ushort HMCAD15_QUAD_CH_1_SLP = (1 << 1);
            const ushort HMCAD15_QUAD_CH_0_SLP = (1 << 0);
            const ushort HMCAD15_PWR_MODE_SLEEP = (1 << 8);
            const ushort HMCAD15_PWR_MODE_POWERDN = (1 << 9);

            switch (powerMode)
            {
                case Hmcad15xxPowerMode.Active:
                    switch (mode)
                    {
                        case Hmcad15xxMode.SingleChannel:
                            data |= channelConfiguration[0].Active ? HMCAD15_SINGLE_CH_SLP : (ushort)0;
                            break;
                        case Hmcad15xxMode.DualChannel:
                            data |= channelConfiguration[0].Active ? HMCAD15_DUAL_CH_0_SLP : (ushort)0;
                            data |= channelConfiguration[1].Active ? HMCAD15_DUAL_CH_1_SLP : (ushort)0;
                            break;
                        case Hmcad15xxMode.QuadChannel:
                            data |= channelConfiguration[0].Active ? HMCAD15_QUAD_CH_0_SLP : (ushort)0;
                            data |= channelConfiguration[1].Active ? HMCAD15_QUAD_CH_1_SLP : (ushort)0;
                            data |= channelConfiguration[2].Active ? HMCAD15_QUAD_CH_2_SLP : (ushort)0;
                            data |= channelConfiguration[3].Active ? HMCAD15_QUAD_CH_3_SLP : (ushort)0;
                            break;
                        default:
                            break;
                    }
                    break;
                case Hmcad15xxPowerMode.Sleep:
                    data = HMCAD15_PWR_MODE_SLEEP;
                    break;
                case Hmcad15xxPowerMode.PowerDown:
                    data = HMCAD15_PWR_MODE_POWERDN;
                    break;
                default:
                    throw new NotImplementedException();
            }

            RegisterWrite(Hmcad15xxRegister.POWER_CTRL, data);
        }

        /// <summary>
        /// Set the channel configuration.
        /// </summary>
        public void SetChannelConfig()
        {
            PowerMode(Hmcad15xxPowerMode.PowerDown);
            ApplySampleMode();
            ApplyChannelGain();
            PowerMode(Hmcad15xxPowerMode.Active);
            ApplyChannelMap();
        }

        /// <summary>
        /// Set the full scale adjustment.
        /// </summary>
        public void FullScaleAdjust(sbyte adjustment)
        {
            const sbyte HMCAD15_FULL_SCALE_MAX = 97;
            const sbyte HMCAD15_FULL_SCALE_MIN = -100;
            ushort HMCAD15_FULL_SCALE_SET(sbyte x) { return (ushort)(((((x + 2) - HMCAD15_FULL_SCALE_MIN) * 0x3F) / (HMCAD15_FULL_SCALE_MAX - HMCAD15_FULL_SCALE_MIN)) & 0x3F); }

            if ((HMCAD15_FULL_SCALE_MAX < adjustment) || (adjustment < HMCAD15_FULL_SCALE_MIN))
            {
                throw new ArgumentOutOfRangeException(nameof(adjustment));
            }

            RegisterWrite(Hmcad15xxRegister.ADC_FULL_SCALE, HMCAD15_FULL_SCALE_SET(adjustment));

            fullScaleAdjust = adjustment;
        }

        /// <summary>
        /// Set a test mode that sends test data out the LVDS interface.
        /// </summary>
        public void SetTestPattern()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Set the sample mode
        /// </summary>
        public void SetSampleMode(uint sample_rate, Hmcad15xxLvdsDataWidth width)
        {
            const uint HMCAD15_SINGLE_LOW_CLK_THRESHOLD = 240000000;
            const uint HMCAD15_DUAL_LOW_CLK_THRESHOLD = 120000000;
            const uint HMCAD15_QUAD_LOW_CLK_THRESHOLD = 60000000;
            const uint HMCAD15_PREC_LOW_CLK_THRESHOLD = 30000000;

            lvdsDataWidth = width;

            if (((mode == Hmcad15xxMode.SingleChannel) && (sample_rate < HMCAD15_SINGLE_LOW_CLK_THRESHOLD)) ||
                ((mode == Hmcad15xxMode.DualChannel) && (sample_rate < HMCAD15_DUAL_LOW_CLK_THRESHOLD)) ||
                ((mode == Hmcad15xxMode.QuadChannel) && (sample_rate < HMCAD15_QUAD_LOW_CLK_THRESHOLD)))
            {
                lowClockFrequency = Hmcad15xxLvdsLowClockFrequency.Active;
            }
            else
            {
                lowClockFrequency = Hmcad15xxLvdsLowClockFrequency.Inactive;
            }

            ApplyLvdsMode();
        }

        private void RegisterWrite(Hmcad15xxRegister register, ushort data)
        {
            byte[] bytes = [(byte)((data >> 8) & 0xFF), (byte)(data & 0xFF)];
            spiDevice.WaitForIdle();
            spiDevice.Write((byte)register, bytes);
            logger.LogTrace("HMCAD SPI R: 0x{register:X2} D: 0x{data:X4}", (byte)register, data);
        }

        private void ApplyLvdsMode()
        {
            // Set LVDS Drive Strength
            ushort data = (ushort)((byte)lvdsDrive & 0x07 | ((byte)lvdsDrive & 0x07) << 4 | ((byte)lvdsDrive & 0x07) << 8);
            RegisterWrite(Hmcad15xxRegister.LVDS_DRIVE, data);

            // Set LVDS DDR Phase
            data = (ushort)((((byte)lvdsPhase) & 0x03) << 5);
            RegisterWrite(Hmcad15xxRegister.LCLK_PHASE, data);

            // Set LVDS Data Width (bits per sample)
            data = (ushort)((((byte)lvdsDataWidth) & 0x07) | ((((byte)lowClockFrequency) & 0x01) << 3));
            RegisterWrite(Hmcad15xxRegister.LVDS_MISC, data);

            // Set output data binary twos complement format
            data = (ushort)((((byte)dataBtcFormat) & 0x1) << 2);
            RegisterWrite(Hmcad15xxRegister.DATA_FMT, data);
        }

        private void ApplySampleMode()
        {
            switch (mode)
            {
                case Hmcad15xxMode.SingleChannel:
                    clockDiv = Hmcad15xxClockDiv.Div1;
                    break;
                case Hmcad15xxMode.DualChannel:
                    clockDiv = Hmcad15xxClockDiv.Div2;
                    break;
                case Hmcad15xxMode.QuadChannel:
                    clockDiv = Hmcad15xxClockDiv.Div4;
                    break;
                case Hmcad15xxMode.QuadChannel14bit:
                    // Might need to set an additional bit? Refer to datasheet.
                    throw new NotImplementedException();
            }

            ushort data = (ushort)(((byte)mode & 0x0F) | ((((byte)clockDiv) & 0x03) << 8));
            RegisterWrite(Hmcad15xxRegister.CHAN_MODE, data);
        }

        private void ApplyChannelMap()
        {
            ushort in12 = 0, in34 = 0, inv = 0;

            ushort HMCAD15_SEL_CH_1(byte x) { return (ushort)(((x) & 0x0F) << 1); }
            ushort HMCAD15_SEL_CH_2(byte x) { return (ushort)(((x) & 0x0F) << 9); }
            ushort HMCAD15_SEL_CH_3(byte x) { return (ushort)(((x) & 0x0F) << 1); }
            ushort HMCAD15_SEL_CH_4(byte x) { return (ushort)(((x) & 0x0F) << 9); }
            ushort HMCAD15_CH_INVERT_Q1(bool x) { return (ushort)((x ? 1 : 0) << 0); }
            ushort HMCAD15_CH_INVERT_Q2(bool x) { return (ushort)((x ? 1 : 0) << 1); }
            ushort HMCAD15_CH_INVERT_Q3(bool x) { return (ushort)((x ? 1 : 0) << 2); }
            ushort HMCAD15_CH_INVERT_Q4(bool x) { return (ushort)((x ? 1 : 0) << 3); }
            ushort HMCAD15_CH_INVERT_D1(bool x) { return (ushort)((x ? 1 : 0) << 4); }
            ushort HMCAD15_CH_INVERT_D2(bool x) { return (ushort)((x ? 1 : 0) << 5); }
            ushort HMCAD15_CH_INVERT_S1(bool x) { return (ushort)((x ? 1 : 0) << 6); }

            switch (mode)
            {
                case Hmcad15xxMode.SingleChannel:
                    in12 = HMCAD15_SEL_CH_1(channelConfiguration[0].Input);
                    in12 |= HMCAD15_SEL_CH_2(channelConfiguration[0].Input);
                    in34 = HMCAD15_SEL_CH_3(channelConfiguration[0].Input);
                    in34 |= HMCAD15_SEL_CH_4(channelConfiguration[0].Input);
                    inv = HMCAD15_CH_INVERT_S1(channelConfiguration[0].Invert);
                    break;
                case Hmcad15xxMode.DualChannel:
                    in12 = HMCAD15_SEL_CH_1(channelConfiguration[0].Input);
                    in12 |= HMCAD15_SEL_CH_2(channelConfiguration[0].Input);
                    in34 = HMCAD15_SEL_CH_3(channelConfiguration[1].Input);
                    in34 |= HMCAD15_SEL_CH_4(channelConfiguration[1].Input);
                    inv = HMCAD15_CH_INVERT_D1(channelConfiguration[0].Invert);
                    inv |= HMCAD15_CH_INVERT_D2(channelConfiguration[1].Invert);
                    break;
                case Hmcad15xxMode.QuadChannel:
                    in12 = HMCAD15_SEL_CH_1(channelConfiguration[0].Input);
                    in12 |= HMCAD15_SEL_CH_2(channelConfiguration[1].Input);
                    in34 = HMCAD15_SEL_CH_3(channelConfiguration[2].Input);
                    in34 |= HMCAD15_SEL_CH_4(channelConfiguration[3].Input);
                    inv = HMCAD15_CH_INVERT_Q1(channelConfiguration[0].Invert);
                    inv |= HMCAD15_CH_INVERT_Q2(channelConfiguration[1].Invert);
                    inv |= HMCAD15_CH_INVERT_Q3(channelConfiguration[2].Invert);
                    inv |= HMCAD15_CH_INVERT_Q4(channelConfiguration[3].Invert);
                    break;
            }

            RegisterWrite(Hmcad15xxRegister.IN_SEL_1_2, in12);
            RegisterWrite(Hmcad15xxRegister.IN_SEL_3_4, in34);
            RegisterWrite(Hmcad15xxRegister.CHAN_INVERT, inv);
        }

        private void ApplyChannelGain()
        {
            ushort cgain;

            ushort HMCAD15_CGAIN_Q1(bool x) { return (ushort)((x ? 1 : 0) & 0xF); }
            ushort HMCAD15_CGAIN_Q2(bool x) { return (ushort)(((x ? 1 : 0) & 0xF) << 4); }
            ushort HMCAD15_CGAIN_Q3(bool x) { return (ushort)(((x ? 1 : 0) & 0xF) << 8); }
            ushort HMCAD15_CGAIN_Q4(bool x) { return (ushort)(((x ? 1 : 0) & 0xF) << 12); }
            ushort HMCAD15_CGAIN_D1(bool x) { return (ushort)((x ? 1 : 0) & 0xF); }
            ushort HMCAD15_CGAIN_D2(bool x) { return (ushort)(((x ? 1 : 0) & 0xF) << 4); }
            ushort HMCAD15_CGAIN_S1(bool x) { return (ushort)(((x ? 1 : 0) & 0xF) << 8); }

            switch (mode)
            {
                case Hmcad15xxMode.SingleChannel:
                    cgain = HMCAD15_CGAIN_S1(channelConfiguration[0].Coarse);
                    RegisterWrite(Hmcad15xxRegister.COARSE_GAIN_2, cgain);
                    break;
                case Hmcad15xxMode.DualChannel:
                    cgain = HMCAD15_CGAIN_D1(channelConfiguration[0].Coarse);
                    cgain |= HMCAD15_CGAIN_D2(channelConfiguration[1].Coarse);
                    RegisterWrite(Hmcad15xxRegister.COARSE_GAIN_2, cgain);
                    break;
                case Hmcad15xxMode.QuadChannel:
                    cgain = HMCAD15_CGAIN_Q1(channelConfiguration[0].Coarse);
                    cgain |= HMCAD15_CGAIN_Q2(channelConfiguration[1].Coarse);
                    cgain |= HMCAD15_CGAIN_Q3(channelConfiguration[2].Coarse);
                    cgain |= HMCAD15_CGAIN_Q4(channelConfiguration[3].Coarse);
                    RegisterWrite(Hmcad15xxRegister.COARSE_GAIN_1, cgain);
                    break;
            }
        }
    }
}
