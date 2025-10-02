namespace TS.NET.Engine;

public abstract record ProcessingRequestDto();

// Control
public record ProcessingRunDto() : ProcessingRequestDto, INotificationDto;
public record ProcessingStopDto() : ProcessingRequestDto, INotificationDto;
public record ProcessingForceDto() : ProcessingRequestDto, INotificationDto;
public record ProcessingSetModeDto(Mode Mode) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetDepthDto(int Samples) : ProcessingRequestDto, INotificationDto;

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
public record ProcessingSetTriggerSourceDto(TriggerChannel Channel) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerTypeDto(TriggerType Type) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerDelayDto(ulong Femtoseconds) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerHoldoffDto(ulong Femtoseconds) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerInterpolationDto(bool Enabled) : ProcessingRequestDto, INotificationDto;

// EdgeTriggerParameters { Level = 0, Hysteresis = 5, Direction = EdgeDirection.Rising }
public record ProcessingSetEdgeTriggerLevelDto(double LevelVolts) : ProcessingRequestDto, INotificationDto;
//public record ProcessingSetEdgeTriggerHysteresisDto(int Hysteresis) : ProcessingRequestDto;
public record ProcessingSetEdgeTriggerDirectionDto(EdgeDirection Edge) : ProcessingRequestDto, INotificationDto;

// BurstTriggerParameters { WindowHighLevel = 64, WindowLowLevel = -64, MinimumInRangePeriod = 450000 }
// ...

public record ProcessingSetBoxcarFilter(BoxcarAveraging Averages) : ProcessingRequestDto;
