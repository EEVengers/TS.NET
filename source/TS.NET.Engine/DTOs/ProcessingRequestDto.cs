namespace TS.NET.Engine;

public abstract record ProcessingRequestDto();

// Only DTOs that generate a response, have "Request" in the name. 

// State
public record ProcessingRun() : ProcessingRequestDto, INotificationDto;
public record ProcessingStop() : ProcessingRequestDto, INotificationDto;
public record ProcessingForce() : ProcessingRequestDto, INotificationDto;

public record ProcessingGetRatesRequest() : ProcessingRequestDto;       // A collection of possible logical rates depending on hardware configuration, not an exhaustive list of all rates

// Set
public record ProcessingSetMode(Mode Mode) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetDepth(int Samples) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetRate(ulong Rate) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetResolution(AdcResolution Resolution) : ProcessingRequestDto, INotificationDto;

public record ProcessingSetEnabled(int ChannelIndex, bool Enabled) : ProcessingRequestDto, INotificationDto;

public record ProcessingSetTriggerSource(TriggerChannel Channel) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerType(TriggerType Type) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerDelay(ulong Femtoseconds) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerHoldoff(ulong Femtoseconds) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerInterpolation(bool Enabled) : ProcessingRequestDto, INotificationDto;

public record ProcessingSetEdgeTriggerLevel(float LevelVolts) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetEdgeTriggerDirection(EdgeDirection Edge) : ProcessingRequestDto, INotificationDto;

// Get
public record ProcessingGetStateRequest() : ProcessingRequestDto;

public record ProcessingGetModeRequest() : ProcessingRequestDto;
public record ProcessingGetDepthRequest() : ProcessingRequestDto;
public record ProcessingGetRateRequest() : ProcessingRequestDto;
public record ProcessingGetResolutionRequest() : ProcessingRequestDto;

public record ProcessingGetTriggerSourceRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerTypeRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerDelayRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerHoldoffRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerInterpolationRequest() : ProcessingRequestDto;

public record ProcessingGetEdgeTriggerLevelRequest() : ProcessingRequestDto;
public record ProcessingGetEdgeTriggerDirectionRequest() : ProcessingRequestDto;
