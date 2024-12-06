namespace TS.NET.Engine
{
    public abstract record ProcessingRequestDto();

    // Trigger actions
    public record ProcessingRunDto() : ProcessingRequestDto;
    public record ProcessingStopDto() : ProcessingRequestDto;
    public record ProcessingForceTriggerDto() : ProcessingRequestDto;

    // Trigger configuration
    public record ProcessingSetTriggerModeDto(TriggerMode Mode) : ProcessingRequestDto;
    public record ProcessingSetTriggerSourceDto(TriggerChannel Channel) : ProcessingRequestDto;
    public record ProcessingSetTriggerTypeDto(TriggerType Type) : ProcessingRequestDto;
    public record ProcessingSetTriggerDelayDto(ulong Femtoseconds) : ProcessingRequestDto;
    public record ProcessingSetTriggerHoldoffDto(ulong Femtoseconds) : ProcessingRequestDto;
    public record ProcessingSetTriggerInterpolationDto(bool Enabled) : ProcessingRequestDto;

    // Edge trigger parameters
    public record ProcessingSetEdgeTriggerLevelDto(double LevelVolts) : ProcessingRequestDto;
    // ProcessingSetEdgeTriggerHysteresisDto;
    public record ProcessingSetEdgeTriggerDirectionDto(EdgeDirection Edge) : ProcessingRequestDto;

    public record ProcessingSetDepthDto(int Samples) : ProcessingRequestDto;
}
