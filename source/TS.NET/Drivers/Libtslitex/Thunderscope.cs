using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.Libtslitex
{
    public class Thunderscope : IThunderscope
    {
        private readonly ILogger logger;
        private bool open = false;
        private nint tsHandle;
        private uint readSegmentLengthBytes;

        private bool[] channelEnabled;
        private bool[] channelManualOverride;
        private ThunderscopeChannelCalibration[] channelCalibration;
        private ThunderscopeChannelFrontend[] channelFrontend;
        private ThunderscopeLiteXStatus health;

        /// <summary>
        /// readSegmentLengthBytes should be the same as DMA_BUFFER_SIZE in the driver. Other values may work, further research needed.
        /// </summary>
        public Thunderscope(ILoggerFactory loggerFactory, int readSegmentLengthBytes)
        {
            if (ThunderscopeMemory.Length % readSegmentLengthBytes != 0)
                throw new ArgumentException("ThunderscopeMemory.Length % readSegmentLengthBytes != 0");
            this.readSegmentLengthBytes = (uint)readSegmentLengthBytes;
            logger = loggerFactory.CreateLogger("Driver.LiteX");
            channelEnabled = new bool[4];
            channelManualOverride = new bool[4];
            channelCalibration = new ThunderscopeChannelCalibration[4];
            channelFrontend = new ThunderscopeChannelFrontend[4];
            health = new ThunderscopeLiteXStatus();
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
            SetRate(initialHardwareConfiguration.SampleRateHz);
            SetResolution(initialHardwareConfiguration.Resolution);

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

            //var channel = new ThunderscopeChannelFrontend();
            //var tsChannel = new Interop.tsChannelParam_t();

            //var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
            //if (retVal != 0)
            //    throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            //channel.RequestedVoltFullScale = requestedChannelVoltScale[channelIndex];
            //channel.ActualVoltFullScale = (double)tsChannel.volt_scale_uV / 1000000.0;
            //channel.RequestedVoltOffset = requestedChannelVoltOffset[channelIndex];
            //channel.ActualVoltOffset = (double)tsChannel.volt_offset_uV / 1000000.0;
            //channel.Coupling = (tsChannel.coupling == 1) ? ThunderscopeCoupling.AC : ThunderscopeCoupling.DC;
            //channel.Termination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
            //channel.Bandwidth = (tsChannel.bandwidth == 750) ? ThunderscopeBandwidth.Bw750M :
            //                        (tsChannel.bandwidth == 650) ? ThunderscopeBandwidth.Bw650M :
            //                        (tsChannel.bandwidth == 350) ? ThunderscopeBandwidth.Bw350M :
            //                        (tsChannel.bandwidth == 200) ? ThunderscopeBandwidth.Bw200M :
            //                        (tsChannel.bandwidth == 100) ? ThunderscopeBandwidth.Bw100M :
            //                        (tsChannel.bandwidth == 20) ? ThunderscopeBandwidth.Bw20M :
            //                        ThunderscopeBandwidth.BwFull;

            //return channel;


            var channel = channelFrontend[channelIndex];
            var tsChannel = new Interop.tsChannelParam_t();

            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
            if (retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");
            channel.Termination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
            return channel;
        }

        public ThunderscopeHardwareConfig GetConfiguration()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var config = new ThunderscopeHardwareConfig();
            var channelCount = 0;

            for (int channelIndex = 0; channelIndex < 4; channelIndex++)
            {
                config.Frontend[channelIndex] = GetChannelFrontend(channelIndex);

                var tsChannel = new Interop.tsChannelParam_t();
                var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
                if (retVal != 0)
                    throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

                if (tsChannel.active == 1)
                {
                    config.EnabledChannels |= (byte)(1 << channelIndex);
                    channelCount++;
                }
            }

            config.AdcChannelMode = (channelCount == 1) ? AdcChannelMode.Single :
                                    (channelCount == 2) ? AdcChannelMode.Dual :
                                    AdcChannelMode.Quad;

            GetStatus();
            config.SampleRateHz = health.AdcSampleRate;

            return config;
        }

        public ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if (channelIndex >= 4 || channelIndex < 0)
                throw new Exception($"Invalid Channel Index {channelIndex}");

            return channelCalibration[channelIndex];
        }

        public ThunderscopeLiteXStatus GetStatus()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var litexState = new Interop.tsScopeState_t();
            if (Interop.GetStatus(tsHandle, out litexState) != 0)
                throw new Exception("Failed to get libtslitex status");

            health.AdcSampleRate = litexState.adc_sample_rate;
            health.AdcSampleSize = litexState.adc_sample_bits;
            health.AdcSampleResolution = litexState.adc_sample_resolution;
            health.AdcSamplesLost = litexState.adc_lost_buffer_count;
            health.FpgaTemp = litexState.temp_c / 1000.0;
            health.VccInt = litexState.vcc_int / 1000.0;
            health.VccAux = litexState.vcc_aux / 1000.0;
            health.VccBram = litexState.vcc_bram / 1000.0;

            // Update active frontends if rate changed as gain will change.
            // Only update a frontend if the manual override isn't active.
            if(cachedSampleRateHz != health.AdcSampleRate)
            {
                cachedSampleRateHz = health.AdcSampleRate;
                for (int i = 0; i < channelFrontend.Length; i++)
                {
                    if (channelEnabled[i] && !channelManualOverride[i])
                        SetChannelFrontend(i, channelFrontend[i]);
                }
            }

            return health;
        }

        uint cachedSampleRateHz = 1000000000;
        AdcResolution cachedSampleResolution = AdcResolution.EightBit;
        public void SetRate(ulong sampleRateHz)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            //cachedSampleRateHz = (uint)sampleRateHz;  // Do not do this, GetStatus will do this and handle the frontends
            uint resolutionValue = cachedSampleResolution switch { AdcResolution.EightBit => 256, AdcResolution.TwelveBit => 4096, _ => throw new NotImplementedException() };
            var retVal = Interop.SetSampleMode(tsHandle, (uint)sampleRateHz, resolutionValue);

            // GetStatus is crucial for managing the rate vs. gain relationship.
            GetStatus();

            if (retVal == -2) //Invalid Parameter
                logger.LogTrace($"Thunderscope failed to set sample rate ({sampleRateHz}): INVALID_PARAMETER");
            else if (retVal < 0)
                throw new Exception($"Thunderscope had an errors trying to set sample rate {sampleRateHz} ({retVal})");
        }

        public void SetResolution(AdcResolution resolution)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            cachedSampleResolution = resolution;
            uint resolutionValue = cachedSampleResolution switch { AdcResolution.EightBit => 256, AdcResolution.TwelveBit => 4096, _ => throw new NotImplementedException() };
            var retVal = Interop.SetSampleMode(tsHandle, cachedSampleRateHz, resolutionValue);

            if (retVal == -2) //Invalid Parameter
                logger.LogTrace($"Thunderscope failed to set resolution ({resolutionValue}): INVALID_PARAMETER");
            else if (retVal < 0)
                throw new Exception($"Thunderscope had an errors trying to set resolution {resolutionValue} ({retVal})");
        }

        public void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            //var tsChannel = new Interop.tsChannelParam_t();
            //var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);

            //if (retVal != 0)
            //    throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            //tsChannel.volt_scale_uV = (uint)(channel.RequestedVoltFullScale * 1000000);
            //tsChannel.volt_offset_uV = (int)(channel.RequestedVoltOffset * 1000000);
            //tsChannel.coupling = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)0 : (byte)1;
            //tsChannel.term = (channel.Termination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
            //tsChannel.bandwidth = channel.Bandwidth switch
            //{
            //    ThunderscopeBandwidth.BwFull => 900,
            //    ThunderscopeBandwidth.Bw750M => 750,
            //    ThunderscopeBandwidth.Bw650M => 650,
            //    ThunderscopeBandwidth.Bw350M => 350,
            //    ThunderscopeBandwidth.Bw200M => 200,
            //    ThunderscopeBandwidth.Bw100M => 100,
            //    ThunderscopeBandwidth.Bw20M => 20,
            //    _ => throw new NotImplementedException()
            //};

            //retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);

            //if (retVal != 0)
            //    throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");

            //requestedChannelVoltScale[channelIndex] = channel.RequestedVoltFullScale;
            //requestedChannelVoltOffset[channelIndex] = channel.RequestedVoltOffset;

            // To calculate:
            //channel.ActualVoltOffset;

            double CalculateInputVpp(ThunderscopeChannelPathCalibration path)
            {
                var gain = channelCalibration[channelIndex].PgaLoadScales[cachedSampleRateHz].Single;
                return path.BufferInputVpp * (1.0/gain);
            }

            bool pathFound = false;
            ThunderscopeChannelPathCalibration selectedPath = new();
            bool attenuator = false;
            while (true)
            {
                // Scan through the frontend gain configurations until the actual volt range would exceed the requested voltage range
                // Pga with no attenuator
                foreach (var path in channelCalibration[channelIndex].Paths)
                {
                    var potentialVpp = CalculateInputVpp(path);
                    if (potentialVpp > channel.RequestedVoltFullScale)
                    {
                        pathFound = true;
                        selectedPath = path;
                        channel.ActualVoltFullScale = potentialVpp;
                        channel.ActualVoltOffset = 0;       // To do
                        break;
                    }
                }
                if (pathFound)
                    break;
                if (channel.Termination == ThunderscopeTermination.FiftyOhm)
                    break;
                attenuator = true;
                // Pga with 1M attenuator
                foreach (var path in channelCalibration[channelIndex].Paths)
                {
                    var potentialVpp = CalculateInputVpp(path) / channelCalibration[channelIndex].AttenuatorScale;
                    if (potentialVpp > channel.RequestedVoltFullScale)
                    {
                        pathFound = true;
                        selectedPath = path;
                        channel.ActualVoltFullScale = potentialVpp;
                        channel.ActualVoltOffset = 0;       // To do
                        break;
                    }
                }
                break;
            }

            if (!pathFound)
            {
                logger.LogError("No valid frontend configuration found");
                return;
            }

            // Note: PGA input voltage should not go beyond +/-0.6V from 2.5V so that enforces a limit in some gain scenarios. 
            //   Datasheet says +/-0.6V. Testing shows up to +/-1.3V. Use datasheet specification.
            var dacValueMaxDeviation = (int)((0.6 - (CalculateInputVpp(selectedPath) / 2.0)) / selectedPath.TrimOffsetDacScaleV);

            // Note: attenuator is the only source of gainFactor change. Probe scaling should be accounted for at the UI level.
            double gainFactor = 1.0;
            if (attenuator)
                gainFactor = channelCalibration[channelIndex].AttenuatorScale;

            // Note: if desired offset is beyond acceptable range for PGA input voltage limits, clamp it.
            // -1 to make the SCPI API match most scope vendors, i.e. if input signal has 100mV offset, send CHAN1:OFFS 0.1 to cancel it out.
            var dacOffset = -1 * (int)((channel.RequestedVoltOffset*gainFactor) / selectedPath.TrimOffsetDacScaleV);
            if (dacOffset > dacValueMaxDeviation)
                dacOffset = dacValueMaxDeviation;
            if (dacOffset < -dacValueMaxDeviation)
                dacOffset = -dacValueMaxDeviation;
            var dacValue = selectedPath.TrimOffsetDacZero - dacOffset;

            // Note: last resort clamping of DAC value.
            if(dacValue < 0) dacValue = 0;
            if (dacValue > 4095) dacValue = 4095;

            // Note: calculate actual offset so UI can use it.
            channel.ActualVoltOffset = ((dacValue - selectedPath.TrimOffsetDacZero) * selectedPath.TrimOffsetDacScaleV)/ gainFactor;

            var manualControl = new ThunderscopeChannelFrontendManualControl()
            {
                Coupling = channel.Coupling,
                Termination = channel.Termination,
                Attenuator = attenuator ? (byte)1 : (byte)0,
                DAC = (ushort)dacValue,
                DPOT = selectedPath.TrimScaleDac,

                PgaLadderAttenuation = selectedPath.PgaLadderAttenuator,
                PgaFilter = channel.Bandwidth,
                PgaHighGain = (selectedPath.PgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
            };
            SetChannelManualControl(channelIndex, manualControl);
            channelManualOverride[channelIndex] = false;            // SetChannelManualControl sets to true, so immediately set to false
            channelFrontend[channelIndex] = channel;
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
                //buffer_uV = (int)(channelCalibration.BufferOffset * 1000000),
                //bias_uV = (int)(channelCalibration.BiasVoltage * 1000000),
                //attenuatorGain1M_mdB = (int)(channelCalibration.AttenuatorGain1MOhm * 1000),
                //attenuatorGain50_mdB = (int)(channelCalibration.AttenuatorGain50Ohm * 1000),
                //bufferGain_mdB = (int)(channelCalibration.BufferGain * 1000),
                //trimRheostat_range = (int)channelCalibration.TrimResistorOhms,
                //preampLowGainError_mdB = (int)(channelCalibration.PgaLowGainError * 1000),
                //preampHighGainError_mdB = (int)(channelCalibration.PgaHighGainError * 1000),
                //preampLowOffset_uV = (int)(channelCalibration.PgaLowOffsetVoltage * 1000000),
                //preampHighOffset_uV = (int)(channelCalibration.PgaHighOffsetVoltage * 1000000),
                //preampOutputGainError_mdB = (int)(channelCalibration.PgaOutputGainError * 1000),
                //preampInputBias_uA = (int)channelCalibration.PgaInputBiasCurrent,
            };
            //tsCal.preampAttenuatorGain_mdB[0] = (int)channelCalibration.PgaAttenuatorGain0;
            //tsCal.preampAttenuatorGain_mdB[1] = (int)channelCalibration.PgaAttenuatorGain1;
            //tsCal.preampAttenuatorGain_mdB[2] = (int)channelCalibration.PgaAttenuatorGain2;
            //tsCal.preampAttenuatorGain_mdB[3] = (int)channelCalibration.PgaAttenuatorGain3;
            //tsCal.preampAttenuatorGain_mdB[4] = (int)channelCalibration.PgaAttenuatorGain4;
            //tsCal.preampAttenuatorGain_mdB[5] = (int)channelCalibration.PgaAttenuatorGain5;
            //tsCal.preampAttenuatorGain_mdB[6] = (int)channelCalibration.PgaAttenuatorGain6;
            //tsCal.preampAttenuatorGain_mdB[7] = (int)channelCalibration.PgaAttenuatorGain7;
            //tsCal.preampAttenuatorGain_mdB[8] = (int)channelCalibration.PgaAttenuatorGain8;
            //tsCal.preampAttenuatorGain_mdB[9] = (int)channelCalibration.PgaAttenuatorGain9;
            //tsCal.preampAttenuatorGain_mdB[10] = (int)channelCalibration.PgaAttenuatorGain10;

            this.channelCalibration[channelIndex] = channelCalibration;
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

            channelEnabled[channelIndex] = enabled;

            // Hardware may need to coerce sample rate so get potential new value
            GetStatus();
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
            tsChannel.pga_bw = (byte)channel.PgaFilter;

            var retVal = Interop.SetChannelManualControl(tsHandle, (uint)channelIndex, tsChannel);

            if (retVal != 0)
                throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");

            channelManualOverride[channelIndex] = true;
        }
    }
}
