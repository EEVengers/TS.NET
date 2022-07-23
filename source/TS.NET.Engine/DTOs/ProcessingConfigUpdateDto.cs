using System;

namespace TS.NET.Engine
{
    public enum ProcessingConfigUpdateCommand
    {
        ForceTrigger = 1
    }

    public record ProcessingConfigUpdateDto(ProcessingConfigUpdateCommand Command);
}
