using System;

namespace TS.NET.Engine
{
    public enum ProcessingRequestCommand
    {
        ForceTrigger = 1
    }

    public record ProcessingRequestDto(ProcessingRequestCommand Command);
}
