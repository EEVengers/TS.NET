using TS.NET.Driver.LiteX;

using Microsoft.Win32.SafeHandles;

namespace TS.NET.Driver.LiteX
{
    public class Thunderscope : IThunderscope
    {
        private ThunderscopeCalibration calibration;
        private string revision;
        private bool open = false;
        private nint tsHandle;

        public void Open(uint devIndex, ThunderscopeCalibration calibration, string revision)
        {
            if (open)
                Close();

            this.calibration = calibration;
            this.revision = revision;

            //Initialise();
            tsHandle = Interop.Open(devIndex);

            if(tsHandle == 0)
                throw new Exception($"Thunderscope failed to open device {devIndex} ({tsHandle})");
            open = true;
        }

        public void Close()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            open = false;
            int returnValue = Interop.Close(tsHandle);
            if(returnValue != 0)
                throw new Exception($"Thunderscope failed closing device ({returnValue})");

        }

        public void Start()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if(Interop.DataEnable(tsHandle, 1) != 0)
                throw new Exception("Thunderscope could not start sample data");
        }

        public void Stop()
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            if(Interop.DataEnable(tsHandle, 1) != 0)
                throw new Exception("");
        }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            unsafe
            {
                int readLen = Interop.Read(tsHandle, data.Pointer, ThunderscopeMemory.Length);
                
                if (readLen < 0)
                    throw new Exception($"Thunderscope failed to read samples ({readLen})");
            }
        }

        public ThunderscopeChannel GetChannel(int channelIndex)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var channel = new ThunderscopeChannel();
            var tsChannel = new Interop.tsChannelParam_t();

            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, ref tsChannel);
            if ( retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");
            
            channel.VoltFullScale = channel.ActualVoltFullScale = (double)tsChannel.volt_scale_mV / 1000.0;
            channel.VoltOffset = (double)tsChannel.volt_offset_mV / 1000.0;
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
            
            for(int ch=0; ch < 4; ch++)
            {
                var tsChannel = new Interop.tsChannelParam_t();

                var retVal = Interop.GetChannelConfig(tsHandle, (uint)ch, ref tsChannel);
                if ( retVal != 0)
                    throw new Exception($"Thunderscope failed to get channel {ch} config ({retVal})");
                
                config.Channels[ch].VoltFullScale = config.Channels[ch].ActualVoltFullScale = (double)tsChannel.volt_scale_mV / 1000.0;
                config.Channels[ch].VoltOffset = (double)tsChannel.volt_offset_mV / 1000.0;
                config.Channels[ch].Coupling = (tsChannel.coupling == 1) ? ThunderscopeCoupling.AC : ThunderscopeCoupling.DC;
                config.Channels[ch].Termination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
                config.Channels[ch].Bandwidth = (tsChannel.bandwidth == 750) ? ThunderscopeBandwidth.Bw750M :
                                        (tsChannel.bandwidth == 650) ? ThunderscopeBandwidth.Bw650M :
                                        (tsChannel.bandwidth == 350) ? ThunderscopeBandwidth.Bw350M :
                                        (tsChannel.bandwidth == 200) ? ThunderscopeBandwidth.Bw200M :
                                        (tsChannel.bandwidth == 100) ? ThunderscopeBandwidth.Bw100M :
                                        (tsChannel.bandwidth == 20) ? ThunderscopeBandwidth.Bw20M :
                                        ThunderscopeBandwidth.BwFull;
                if(tsChannel.active == 1)
                {
                    config.EnabledChannels |= (byte)(1 << ch);
                    channelCount++;
                }
            }

            config.AdcChannelMode = (channelCount == 1) ? AdcChannelMode.Single :
                                    (channelCount == 2) ? AdcChannelMode.Single :
                                    AdcChannelMode.Quad;

            return config;
        }

        public void SetChannel(int channelIndex, ThunderscopeChannel channel)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var tsChannel = new Interop.tsChannelParam_t();
            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, ref tsChannel);

            if ( retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            tsChannel.volt_scale_mV = (uint)(channel.VoltFullScale * 1000);
            tsChannel.volt_offset_mV = (int)(channel.VoltOffset * 1000);
            tsChannel.coupling = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)0 : (byte)1;
            tsChannel.term = (channel.Termination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
            tsChannel.bandwidth = (channel.Bandwidth == ThunderscopeBandwidth.Bw750M) ? (uint)750 :
                                    (channel.Bandwidth == ThunderscopeBandwidth.Bw650M) ? (uint)650 :
                                    (channel.Bandwidth == ThunderscopeBandwidth.Bw350M) ? (uint)350 :
                                    (channel.Bandwidth == ThunderscopeBandwidth.Bw200M) ? (uint)200 :
                                    (channel.Bandwidth == ThunderscopeBandwidth.Bw100M) ? (uint)100 :
                                    (channel.Bandwidth == ThunderscopeBandwidth.Bw20M) ? (uint)20 : (uint)0;
            
            retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, ref tsChannel);

            if ( retVal != 0)
                throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");
        }

        public void SetChannelEnable(int channelIndex, bool enabled)
        {
            if (!open)
                throw new Exception("Thunderscope not open");

            var tsChannel = new Interop.tsChannelParam_t();
            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, ref tsChannel);

            if ( retVal != 0)
                throw new Exception($"Thunderscope failed to get channel {channelIndex} config ({retVal})");

            tsChannel.active = enabled ? (byte)1 : (byte)0;

            retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, ref tsChannel);

            if ( retVal != 0)
                throw new Exception($"Thunderscope failed to set channel {channelIndex} config ({retVal})");

        }
    }
}
