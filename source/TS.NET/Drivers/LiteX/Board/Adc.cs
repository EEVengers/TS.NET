using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX.Control
{
    internal class Adc
    {
        internal enum AdcShuffleMode { S1Channel = 0, S2Channel = 1, S4Channel = 2 }

        const sbyte TS_ADC_FULL_SCALE_ADJUST_DEFAULT = 0x20;  // Full Scale Adjust set 2V

        private readonly ILogger logger;
        private readonly LitePcie litePcie;
        private readonly Hmcad15xxSpiDevice hmcad15xxSpiDevice;

        public Adc(ILoggerFactory loggerFactory, LitePcie litePcie, Hmcad15xxSpiDevice hmcad15xxSpiDevice)
        {
            logger = loggerFactory.CreateLogger(nameof(Adc));
            this.litePcie = litePcie;
            this.hmcad15xxSpiDevice = hmcad15xxSpiDevice;
        }

        public void Init()
        {
            hmcad15xxSpiDevice.Init();
            litePcie.WriteL((uint)CSR.CSR_ADC_HAD1511_CONTROL_ADDR, 1 << CSR.CSR_ADC_HAD1511_CONTROL_FRAME_RST_OFFSET);
            litePcie.WriteL((uint)CSR.CSR_ADC_HAD1511_DOWNSAMPLING_ADDR, 1);
            hmcad15xxSpiDevice.FullScaleAdjust(TS_ADC_FULL_SCALE_ADJUST_DEFAULT);
        }

        public void SetChannelConfiguration(byte channelIndex, byte input, bool invert)
        {
            if (channelIndex > 3) return;

            hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Input = input;
            hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Invert = invert;

            if (hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Active)
            {
                //for (int i = 0; i < 4; i++)
                //{
                //    if (hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Input == hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Input)
                //    {
                //        adc->adcDev.channelCfg[i].invert = invert;
                //        break;
                //    }
                //}
                hmcad15xxSpiDevice.SetChannelConfig();
                litePcie.WriteL((uint)CSR.CSR_ADC_HAD1511_CONTROL_ADDR, 1 << CSR.CSR_ADC_HAD1511_CONTROL_FRAME_RST_OFFSET);
            }
        }

        public void SetChannelEnable(byte channelIndex, bool enable)
        {
            if (channelIndex > 3) return;

            hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Active = enable;

            int activeCount = 0;
            AdcShuffleMode shuffleMode;
            for (int i = 0; i < 4; i++)
            {
                if (hmcad15xxSpiDevice.ChannelConfiguration[i].Active)
                {
                    // Copy Active Channel Configs to ADC in order
                    //LOG_DEBUG("Enabling IN %d as CH %d", activeCount, i);
                    //adc->adcDev.channelCfg[activeCount] = adc->tsChannels[i];
                    activeCount++;
                }
                //else
                //{
                //    //Disable Unused channels in config
                //    //LOG_DEBUG("Disable CH %d", i);
                //    adc->adcDev.channelCfg[TS_NUM_CHANNELS - inactiveCount] = adc->tsChannels[i];
                //    adc->adcDev.channelCfg[TS_NUM_CHANNELS - inactiveCount].active = 0;
                //    inactiveCount++;
                //}
            }

            if (activeCount == 0)
            {
                hmcad15xxSpiDevice.PowerMode(Hmcad15xxPowerMode.Sleep);
            }
            else
            {

                if (activeCount == 1)
                {
                    // adc->adcDev.mode = HMCAD15_SINGLE_CHANNEL;
                    shuffleMode = AdcShuffleMode.S1Channel;
                }
                else if (activeCount == 2)
                {
                    //adc->adcDev.mode = HMCAD15_DUAL_CHANNEL;
                    shuffleMode = AdcShuffleMode.S2Channel;
                }
                else
                {
                    //adc->adcDev.mode = HMCAD15_QUAD_CHANNEL;
                    shuffleMode = AdcShuffleMode.S4Channel;
                }
                hmcad15xxSpiDevice.SetChannelConfig();

                litePcie.WriteL((uint)CSR.CSR_ADC_HAD1511_CONTROL_ADDR, 1 << CSR.CSR_ADC_HAD1511_CONTROL_FRAME_RST_OFFSET);
                litePcie.WriteL((uint)CSR.CSR_ADC_HAD1511_DATA_CHANNELS_ADDR, (uint)shuffleMode << CSR.CSR_ADC_HAD1511_DATA_CHANNELS_SHUFFLE_OFFSET);
            }
        }

        public void SetChannelGain(byte channelIndex, int gainCoarse, int gainFine)
        {
            // NOP - not needed yet
            //hmcad15xxSpiDevice.ChannelConfiguration[channelIndex].Coarse = (byte)gainCoarse;
        }

        public void Shutdown() { }

        public void Run(bool enable)
        {
            litePcie.WriteL((uint)CSR.CSR_ADC_TRIGGER_CONTROL_ADDR, (uint)(enable ? 1 : 0));
        }
    }
}
