using System;

namespace TS.NET.Engine
{
    internal enum HardwareConfigUpdateCommand
    {
        Start = 1,
        Stop
    }

    internal record HardwareConfigUpdateDto(HardwareConfigUpdateCommand Command);
}
