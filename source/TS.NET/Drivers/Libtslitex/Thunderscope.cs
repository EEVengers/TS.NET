using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.Libtslitex
{
    public class Thunderscope : IThunderscope
    {
        private readonly ILogger logger;
        private bool open = false;
        private nint tsHandle;
        private double[] channel_volt_scale;
        private ThunderscopeLiteXStatus tsHealth;
        private ThunderscopeChannelCalibration[] tsCalibration;
        private uint readSegmentLengthBytes;

        /// <summary>
        /// readSegmentLengthBytes should be the same as DMA_BUFFER_SIZE in the driver. Other values may work, further research needed.
        /// </summary>
        public Thunderscope(ILoggerFactory loggerFactory, int readSegmentLengthBytes)
        {
            if (ThunderscopeMemory.Length % readSegmentLengthBytes != 0)
                throw new ArgumentException("ThunderscopeMemory.Length % readSegmentLengthBytes != 0");
            this.readSegmentLengthBytes = (uint)readSegmentLengthBytes;
            logger = loggerFactory.CreateLogger("Driver.LiteX");
            channel_volt_scale = new double[4];
            tsCalibration = new ThunderscopeChannelCalibration[4];
            tsHealth = new ThunderscopeLiteXStatus();
        }

        ~Thunderscope()
        {
            if (open)
                Close();
        }

        public void Open(uint devIndex, ThunderscopeHardwareConfig initialHardwareConfiguration)
        {
            if (open)
                Close();

            //Initialise();
            tsHandle = Interop.Open(devIndex, false);

            if (tsHandle == 0)
                throw new Exception($"Thunderscope failed to open device {devIndex} ({tsHandle})");
            open = true;
            SetAdcCalibration(initialHardwareConfiguration.AdcCalibration);
            for (int chan = 0; chan < 4; chan++)
            {
                SetChannelCalibration(chan, initialHardwareConfiguration.Calibration[chan]);
                SetChannelFrontend(chan, initialHardwareConfiguration.Frontend[chan]);
                SetChannelEnable(chan, ((initialHardwareConfiguration.EnabledChannels >> chan) & 0x01) > 0);
            }
            GetStatus();        // Required to make SetRate work
            SetRate(initialHardwareConfiguration.SampleRateHz);

            GetStatus();
        }

        public void Close()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            open = false;

            Interop.DataEnable(tsHandle, 0);

            int returnValue = Interop.Close(tsHandle);
            if (returnValue != 0)
                throw new Exception($"Thunderscope failed closing device ({returnValue})");

        }

        public void Start()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if (Interop.DataEnable(tsHandle, 1) != 0)
                throw new Exception("Thunderscope could not start sample data");
        }

        public void Stop()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if (Interop.DataEnable(tsHandle, 0) != 0)
                throw new Exception("");
        }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            unsafe
            {
                ulong length = ThunderscopeMemory.Length;
                ulong dataRead = 0;
                while (length > 0)
                {
                    int readLen = Interop.Read(tsHandle, data.Pointer + dataRead, readSegmentLengthBytes);

                    if (readLen < 0)
                        throw new Exception($"Thunderscope failed to read samples ({readLen})");
                    else if (readLen != readSegmentLengthBytes)
                        throw new Exception($"Thunderscope read incorrect sample length ({readLen})");

                    dataRead += (ulong)readSegmentLengthBytes;
                    length -= (ulong)readSegmentLengthBytes;
                }
            }
        }

        public bool TryRead(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            if (!open)
                return false;

            unsafe
            {
                ulong length = ThunderscopeMemory.Length;
                ulong dataRead = 0;
                while (length > 0)
                {
                    int readLen = Interop.Read(tsHandle, data.Pointer + dataRead, readSegmentLengthBytes);

                    if (readLen < 0)
                        return false;
                    else if (readLen != readSegmentLengthBytes)
                        throw new Exception($"Thunderscope read incorrect sample length ({readLen})");

                    dataRead += (ulong)readSegmentLengthBytes;
                    length -= (ulong)readSegmentLengthBytes;
                }
                return true;
            }
        }

        public ThunderscopeChannelFrontend GetChannelFrontend(int channelIndex)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var channel = new ThunderscopeChannelFrontend();
            var tsChannel = new Interop.tsChannelParam_t();

            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
            if (retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            channel.VoltFullScale = this.channel_volt_scale[channelIndex];
            channel.ActualVoltFullScale = (double)tsChannel.volt_scale_uV / 1000000.0;
            channel.VoltOffset = (double)tsChannel.volt_offset_uV / 1000000.0;
            channel.Coupling = (tsChannel.coupling == 1) ? ThunderscopeCoupling.AC : ThunderscopeCoupling.DC;
            channel.Termination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
            channel.Bandwidth = (tsChannel.bandwidth == 750) ? ThunderscopeBandwidth.Bw750M :
                                    (tsChannel.bandwidth == 650) ? ThunderscopeBandwidth.Bw650M :
                                    (tsChannel.bandwidth == 350) ? ThunderscopeBandwidth.Bw350M :
                                    (tsChannel.bandwidth == 200) ? ThunderscopeBandwidth.Bw200M :
                                    (tsChannel.bandwidth == 100) ? ThunderscopeBandwidth.Bw100M :
                                    (tsChannel.bandwidth == 20) ? ThunderscopeBandwidth.Bw20M :
                                    ThunderscopeBandwidth.BwFull;

            return channel;
        }

        public ThunderscopeHardwareConfig GetConfiguration()
        {

            if (!open)
                throw new Exception("Thunderscope not open");

            var config = new ThunderscopeHardwareConfig();
            var channelCount = 0;

            for (int ch = 0; ch < 4; ch++)
            {
                var tsChannel = new Interop.tsChannelParam_t();

                var retVal = Interop.GetChannelConfig(tsHandle, (uint)ch, out tsChannel);
                if (retVal != 0)
                    throw new Exception($"Thunderscope failed to get channel {ch} config ({retVal})");

                config.Frontend[ch].VoltFullScale = this.channel_volt_scale[ch];
                config.Frontend[ch].ActualVoltFullScale = (double)tsChannel.volt_scale_uV / 1000000.0;
                config.Frontend[ch].VoltOffset = (double)tsChannel.volt_offset_uV / 1000000.0;
                config.Frontend[ch].Coupling = (tsChannel.coupling == 1) ? ThunderscopeCoupling.AC : ThunderscopeCoupling.DC;
                config.Frontend[ch].Termination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
                config.Frontend[ch].Bandwidth = (tsChannel.bandwidth == 750) ? ThunderscopeBandwidth.Bw750M :
                                        (tsChannel.bandwidth == 650) ? ThunderscopeBandwidth.Bw650M :
                                        (tsChannel.bandwidth == 350) ? ThunderscopeBandwidth.Bw350M :
                                        (tsChannel.bandwidth == 200) ? ThunderscopeBandwidth.Bw200M :
                                        (tsChannel.bandwidth == 100) ? ThunderscopeBandwidth.Bw100M :
                                        (tsChannel.bandwidth == 20) ? ThunderscopeBandwidth.Bw20M :
                                        ThunderscopeBandwidth.BwFull;
                if (tsChannel.active == 1)
                {
                    config.EnabledChannels |= (byte)(1 << ch);
                    channelCount++;
                }
            }

            config.AdcChannelMode = (channelCount == 1) ? AdcChannelMode.Single :
                                    (channelCount == 2) ? AdcChannelMode.Dual :
                                    AdcChannelMode.Quad;

            GetStatus();
            config.SampleRateHz = tsHealth.AdcSampleRate;

            return config;
        }

        public ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if (channelIndex >= 4 || channelIndex < 0)
                throw new Exception($"Invalid Channel Index {channelIndex}");

            return tsCalibration[channelIndex];
        }

        public ThunderscopeLiteXStatus GetStatus()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var litexState = new Interop.tsScopeState_t();
            if (Interop.GetStatus(tsHandle, out litexState) != 0)
                throw new Exception("Failed to get libtslitex status");

            tsHealth.AdcSampleRate = litexState.adc_sample_rate;
            tsHealth.AdcSampleSize = litexState.adc_sample_bits;
            tsHealth.AdcSampleResolution = litexState.adc_sample_resolution;
            tsHealth.AdcSamplesLost = litexState.adc_lost_buffer_count;
            tsHealth.FpgaTemp = litexState.temp_c / 1000.0;
            tsHealth.VccInt = litexState.vcc_int / 1000.0;
            tsHealth.VccAux = litexState.vcc_aux / 1000.0;
            tsHealth.VccBram = litexState.vcc_bram / 1000.0;

            return tsHealth;
        }

        public void SetRate(ulong sampleRateHz)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var retVal = Interop.SetSampleMode(tsHandle, (uint)sampleRateHz, tsHealth.AdcSampleResolution);

            if (retVal == -2) //Invalid Parameter
                logger.LogTrace($"Thunderscope failed to set sample rate ({sampleRateHz}): INVALID_PARAMETER");
            else if (retVal < 0)
                throw new Exception($"Thunderscope had an errors trying to set sample rate {sampleRateHz} ({retVal})");
        }

        public void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var tsChannel = new Interop.tsChannelParam_t();
            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);

            if (retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            tsChannel.volt_scale_uV = (uint)(channel.VoltFullScale * 1000000);
            tsChannel.volt_offset_uV = (int)(channel.VoltOffset * 1000000);
            tsChannel.coupling = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)0 : (byte)1;
            tsChannel.term = (channel.Termination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
            tsChannel.bandwidth = channel.Bandwidth switch
            {
                ThunderscopeBandwidth.BwFull => 900,
                ThunderscopeBandwidth.Bw750M => 750,
                ThunderscopeBandwidth.Bw650M => 650,
                ThunderscopeBandwidth.Bw350M => 350,
                ThunderscopeBandwidth.Bw200M => 200,
                ThunderscopeBandwidth.Bw100M => 100,
                ThunderscopeBandwidth.Bw20M => 20,
                _ => throw new NotImplementedException()
            };

            retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);

            if (retVal != 0)
                throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");

            this.channel_volt_scale[channelIndex] = channel.VoltFullScale;
        }

        public void SetAdcCalibration(ThunderscopeAdcCalibration adcCal)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var tsCal = new Interop.tsAdcCalibration_t();
            tsCal.branchFineGain[0] = adcCal.FineGainBranch1;
            tsCal.branchFineGain[1] = adcCal.FineGainBranch2;
            tsCal.branchFineGain[2] = adcCal.FineGainBranch3;
            tsCal.branchFineGain[3] = adcCal.FineGainBranch4;
            tsCal.branchFineGain[4] = adcCal.FineGainBranch5;
            tsCal.branchFineGain[5] = adcCal.FineGainBranch6;
            tsCal.branchFineGain[6] = adcCal.FineGainBranch7;
            tsCal.branchFineGain[7] = adcCal.FineGainBranch8;
            
            Interop.SetAdcCalibration(tsHandle, in tsCal);
        }
        public void SetChannelCalibration(int channelIndex, ThunderscopeChannelCalibration channelCalibration)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if (channelIndex >= 4 || channelIndex < 0)
                throw new Exception($"Invalid Channel Index {channelIndex}");

            var tsCal = new Interop.tsChannelCalibration_t
            {
                buffer_uV = (int)(channelCalibration.BufferOffset * 1000000),
                bias_uV = (int)(channelCalibration.BiasVoltage * 1000000),
                attenuatorGain1M_mdB = (int)(channelCalibration.AttenuatorGain1MOhm * 1000),
                attenuatorGain50_mdB = (int)(channelCalibration.AttenuatorGain50Ohm * 1000),
                bufferGain_mdB = (int)(channelCalibration.BufferGain * 1000),
                trimRheostat_range = (int)channelCalibration.TrimResistorOhms,
                preampLowGainError_mdB = (int)(channelCalibration.PgaLowGainError * 1000),
                preampHighGainError_mdB = (int)(channelCalibration.PgaHighGainError * 1000),
                preampLowOffset_uV = (int)(channelCalibration.PgaLowOffsetVoltage * 1000000),
                preampHighOffset_uV = (int)(channelCalibration.PgaHighOffsetVoltage * 1000000),
                preampOutputGainError_mdB = (int)(channelCalibration.PgaOutputGainError * 1000),
                preampInputBias_uA = (int)channelCalibration.PgaInputBiasCurrent,
            };
            tsCal.preampAttenuatorGain_mdB[0] = (int)channelCalibration.PgaAttenuatorGain0;
            tsCal.preampAttenuatorGain_mdB[1] = (int)channelCalibration.PgaAttenuatorGain1;
            tsCal.preampAttenuatorGain_mdB[2] = (int)channelCalibration.PgaAttenuatorGain2;
            tsCal.preampAttenuatorGain_mdB[3] = (int)channelCalibration.PgaAttenuatorGain3;
            tsCal.preampAttenuatorGain_mdB[4] = (int)channelCalibration.PgaAttenuatorGain4;
            tsCal.preampAttenuatorGain_mdB[5] = (int)channelCalibration.PgaAttenuatorGain5;
            tsCal.preampAttenuatorGain_mdB[6] = (int)channelCalibration.PgaAttenuatorGain6;
            tsCal.preampAttenuatorGain_mdB[7] = (int)channelCalibration.PgaAttenuatorGain7;
            tsCal.preampAttenuatorGain_mdB[8] = (int)channelCalibration.PgaAttenuatorGain8;
            tsCal.preampAttenuatorGain_mdB[9] = (int)channelCalibration.PgaAttenuatorGain9;
            tsCal.preampAttenuatorGain_mdB[10] = (int)channelCalibration.PgaAttenuatorGain10;

            tsCalibration[channelIndex] = channelCalibration;
            Interop.SetCalibration(tsHandle, (uint)channelIndex, in tsCal);
        }

        public void SetChannelEnable(int channelIndex, bool enabled)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var tsChannel = new Interop.tsChannelParam_t();
            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);

            if (retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            tsChannel.active = enabled ? (byte)1 : (byte)0;

            retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);

            if (retVal != 0)
                throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");

        }
   
        public void SetChannelManualControl(int channelIndex, ThunderscopeChannelFrontendManualControl channel)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var tsChannel = new Interop.tsChannelCtrl_t();
            tsChannel.atten = channel.Attenuator;
            tsChannel.term = (channel.Termination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
            tsChannel.dc_couple = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)1 : (byte)0;
            tsChannel.dac = channel.DAC;
            tsChannel.dpot = channel.DPOT;
            
            tsChannel.pga_atten = channel.PgaLadderAttenuation;
            tsChannel.pga_high_gain = channel.PgaHighGain;
            tsChannel.pga_bw = channel.PgaFilter;         

            var retVal = Interop.SetChannelManualControl(tsHandle, (uint)channelIndex, tsChannel);

            if (retVal != 0)
                throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");

        }
    }
}
