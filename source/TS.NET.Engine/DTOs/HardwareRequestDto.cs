using System;

namespace TS.NET.Engine
{
    public enum HardwareRequestCommand
    {
        Start = 1,
        Stop,
        UpdateChannelConfig
    }

    public record HardwareRequestDto(HardwareRequestCommand Command);
}
