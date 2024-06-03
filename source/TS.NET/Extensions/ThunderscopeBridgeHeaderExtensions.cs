namespace TS.NET
{
    public static class ThunderscopeBridgeHeaderExtensions
    {
        public static ThunderscopeChannel GetTriggerChannel(this ThunderscopeHardwareConfig config, TriggerChannel triggerChannel)
        {
            return triggerChannel switch
            {
                TriggerChannel.Channel0 => config.Channels[0],
                TriggerChannel.Channel1 => config.Channels[1],
                TriggerChannel.Channel2 => config.Channels[2],
                TriggerChannel.Channel3 => config.Channels[3],
                _ => throw new NotImplementedException()
            };
        }

        public static bool IsTriggerChannelAnEnabledChannel(this ThunderscopeHardwareConfig config, TriggerChannel triggerChannel)
        {
            return triggerChannel switch
            {
                TriggerChannel.NotSet => false,
                TriggerChannel.Channel0 => (config.EnabledChannels & 0x01) > 0,
                TriggerChannel.Channel1 => ((config.EnabledChannels >> 1) & 0x01) > 0,
                TriggerChannel.Channel2 => ((config.EnabledChannels >> 2) & 0x01) > 0,
                TriggerChannel.Channel3 => ((config.EnabledChannels >> 3) & 0x01) > 0,
                _ => throw new NotImplementedException(),
            };
        }

        public static bool DualChannelModeIsTriggerChannelInFirstPosition(this ThunderscopeHardwareConfig config, TriggerChannel triggerChannel)
        {
            // There will be 2 bits enabled in config.EnabledChannels. If the trigger channel is the rightmost bit, return true.
            return triggerChannel switch
            {
                TriggerChannel.Channel0 => true,
                TriggerChannel.Channel1 => (config.EnabledChannels | 0b00001100) > 0,
                TriggerChannel.Channel2 => (config.EnabledChannels | 0b00001000) > 0,
                TriggerChannel.Channel3 => false,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
