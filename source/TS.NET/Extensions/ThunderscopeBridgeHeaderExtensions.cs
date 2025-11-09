namespace TS.NET
{
    public static class ThunderscopeBridgeHeaderExtensions
    {
        public static ThunderscopeChannelFrontend GetTriggerChannelFrontend(this ThunderscopeHardwareConfig config, TriggerChannel triggerChannel)
        {
            return triggerChannel switch
            {
                TriggerChannel.Channel1 => config.Frontend[0],
                TriggerChannel.Channel2 => config.Frontend[1],
                TriggerChannel.Channel3 => config.Frontend[2],
                TriggerChannel.Channel4 => config.Frontend[3],
                _ => throw new NotImplementedException()
            };
        }

        public static bool IsChannelIndexAnEnabledChannel(this ThunderscopeAcquisitionConfig config, int channelIndex)
        {
            return channelIndex switch
            {
                0 => (config.EnabledChannels & 0x01) > 0,
                1 => ((config.EnabledChannels >> 1) & 0x01) > 0,
                2 => ((config.EnabledChannels >> 2) & 0x01) > 0,
                3 => ((config.EnabledChannels >> 3) & 0x01) > 0,
                _ => throw new NotImplementedException(),
            };
        }

        public static bool IsTriggerChannelAnEnabledChannel(this ThunderscopeAcquisitionConfig config, TriggerChannel triggerChannel)
        {
            return triggerChannel switch
            {
                TriggerChannel.None => false,
                TriggerChannel.Channel1 => (config.EnabledChannels & 0x01) > 0,
                TriggerChannel.Channel2 => ((config.EnabledChannels >> 1) & 0x01) > 0,
                TriggerChannel.Channel3 => ((config.EnabledChannels >> 2) & 0x01) > 0,
                TriggerChannel.Channel4 => ((config.EnabledChannels >> 3) & 0x01) > 0,
                _ => throw new NotImplementedException(),
            };
        }

        public static ushort EnabledChannelsCount(this ThunderscopeAcquisitionConfig config)
        {
            return config.EnabledChannels switch
            {
                0 => 0,
                1 => 1,
                2 => 1,
                3 => 2,
                4 => 1,
                5 => 2,
                6 => 2,
                7 => 3,
                8 => 1,
                9 => 2,
                10 => 2,
                11 => 3,
                12 => 2,
                13 => 3,
                14 => 3,
                15 => 4,
                _ => throw new NotImplementedException()
            };
        }

        public static int GetChannelIndexByCaptureBufferIndex(this ThunderscopeAcquisitionConfig config, int position)
        {
            int counter = 0;
            for (int i = 0; i < 4; i++)
            {
                if (((config.EnabledChannels >> i) & 0x01) > 0)
                {
                    if (counter == position)
                        return i;
                    counter++;
                }
            }
            return 0;
        }

        public static int GetCaptureBufferIndexForTriggerChannel(this ThunderscopeAcquisitionConfig config, TriggerChannel triggerChannel)
        {
            // To do: simplify this horror
            int triggerChannelIndex = ((int)triggerChannel) - 1;
            switch (config.AdcChannelMode)
            {
                case AdcChannelMode.Single:
                    return 0;
                case AdcChannelMode.Dual:
                    for (int i = 0; i < 2; i++)
                    {
                        if (config.GetChannelIndexByCaptureBufferIndex(i) == triggerChannelIndex)
                            return i;
                    }
                    throw new NotImplementedException();
                case AdcChannelMode.Quad:
                    for (int i = 0; i < 4; i++)
                    {
                        if (config.GetChannelIndexByCaptureBufferIndex(i) == triggerChannelIndex)
                            return i;
                    }
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
