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

        public static bool IsTriggerChannelAnEnabledChannel(this ThunderscopeHardwareConfig config, TriggerChannel triggerChannel)
        {
            return triggerChannel switch
            {
                TriggerChannel.NotSet => false,
                TriggerChannel.Channel1 => (config.EnabledChannels & 0x01) > 0,
                TriggerChannel.Channel2 => ((config.EnabledChannels >> 1) & 0x01) > 0,
                TriggerChannel.Channel3 => ((config.EnabledChannels >> 2) & 0x01) > 0,
                TriggerChannel.Channel4 => ((config.EnabledChannels >> 3) & 0x01) > 0,
                _ => throw new NotImplementedException(),
            };
        }

        public static bool DualChannelModeIsTriggerChannelInFirstPosition(this ThunderscopeHardwareConfig config, TriggerChannel triggerChannel)
        {
            // There will be 2 bits enabled in config.EnabledChannels. If the trigger channel is the rightmost bit, return true.
            return triggerChannel switch
            {
                TriggerChannel.Channel1 => true,
                TriggerChannel.Channel2 => (config.EnabledChannels | 0b00001100) > 0,
                TriggerChannel.Channel3 => (config.EnabledChannels | 0b00001000) > 0,
                TriggerChannel.Channel4 => false,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
