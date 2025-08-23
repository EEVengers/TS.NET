namespace TS.NET.Engine
{
    public abstract record ProcessingRequestDto();

    public record ProcessingSetDepthDto(int Samples) : ProcessingRequestDto;

    // Control
    public record ProcessingRunDto() : ProcessingRequestDto;
    public record ProcessingStopDto() : ProcessingRequestDto;
    public record ProcessingForceDto() : ProcessingRequestDto;
    public record ProcessingSetModeDto(Mode Mode) : ProcessingRequestDto;

    public record ProcessingGetStateRequest() : ProcessingRequestDto;
    public record ProcessingGetModeRequest() : ProcessingRequestDto;
    public record ProcessingGetDepthRequest() : ProcessingRequestDto;

    // Trigger query requests
    public record ProcessingGetTriggerSourceRequest() : ProcessingRequestDto;
    public record ProcessingGetTriggerTypeRequest() : ProcessingRequestDto;
    public record ProcessingGetTriggerDelayRequest() : ProcessingRequestDto;
    public record ProcessingGetTriggerHoldoffRequest() : ProcessingRequestDto;
    public record ProcessingGetTriggerInterpolationRequest() : ProcessingRequestDto;

    public record ProcessingGetEdgeTriggerLevelRequest() : ProcessingRequestDto;
    public record ProcessingGetEdgeTriggerDirectionRequest() : ProcessingRequestDto;

    // Trigger configuration
    public record ProcessingSetTriggerSourceDto(TriggerChannel Channel) : ProcessingRequestDto;
    public record ProcessingSetTriggerTypeDto(TriggerType Type) : ProcessingRequestDto;
    public record ProcessingSetTriggerDelayDto(ulong Femtoseconds) : ProcessingRequestDto;
    public record ProcessingSetTriggerHoldoffDto(ulong Femtoseconds) : ProcessingRequestDto;
    public record ProcessingSetTriggerInterpolationDto(bool Enabled) : ProcessingRequestDto;

    // EdgeTriggerParameters { Level = 0, Hysteresis = 5, Direction = EdgeDirection.Rising }
    public record ProcessingSetEdgeTriggerLevelDto(double LevelVolts) : ProcessingRequestDto;
    //public record ProcessingSetEdgeTriggerHysteresisDto(int Hysteresis) : ProcessingRequestDto;
    public record ProcessingSetEdgeTriggerDirectionDto(EdgeDirection Edge) : ProcessingRequestDto;

    // BurstTriggerParameters { WindowHighLevel = 64, WindowLowLevel = -64, MinimumInRangePeriod = 450000 }
    // ...

    public record ProcessingSetBoxcarFilter(BoxcarAveraging Averages) : ProcessingRequestDto;
}
