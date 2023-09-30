using System;

namespace TS.NET.Engine
{
    public abstract record ProcessingRequestDto();

    // Trigger actions
    public record ProcessingStartTriggerDto() : ProcessingRequestDto;
    public record ProcessingStopTriggerDto() : ProcessingRequestDto;
    public record ProcessingSingleTriggerDto() : ProcessingRequestDto;
    public record ProcessingForceTriggerDto() : ProcessingRequestDto;

    // Trigger configuration
    public record ProcessingSetTriggerSourceDto(TriggerChannel Channel) : ProcessingRequestDto;
    public record ProcessingSetTriggerDelayDto(long Femtoseconds) : ProcessingRequestDto;
    public record ProcessingSetTriggerLevelDto(double Level) : ProcessingRequestDto;
    public record ProcessingSetTriggerEdgeDirectionDto() : ProcessingRequestDto;

    public record ProcessingSetDepthDto(ulong Samples) : ProcessingRequestDto;
    public record ProcessingSetRateDto(long SamplingHz) : ProcessingRequestDto;
}
