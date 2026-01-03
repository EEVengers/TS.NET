namespace TS.NET.Engine;

public abstract record ProcessingRequestDto();

// Only DTOs that generate a response, have "Request" in the name. 

// State
public record ProcessingRun() : ProcessingRequestDto, INotificationDto;
public record ProcessingStop() : ProcessingRequestDto, INotificationDto;
public record ProcessingForce() : ProcessingRequestDto, INotificationDto;

// Set
public record ProcessingSetMode(Mode Mode) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetDepth(int Samples) : ProcessingRequestDto, INotificationDto;

public record ProcessingSetTriggerSource(TriggerChannel Channel) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerType(TriggerType Type) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerDelay(ulong Femtoseconds) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerHoldoff(ulong Femtoseconds) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetTriggerInterpolation(bool Enabled) : ProcessingRequestDto, INotificationDto;

public record ProcessingSetEdgeTriggerLevel(float LevelVolts) : ProcessingRequestDto, INotificationDto;
public record ProcessingSetEdgeTriggerDirection(EdgeDirection Edge) : ProcessingRequestDto, INotificationDto;

public record HardwareSetRate(ulong Rate) : ProcessingRequestDto, INotificationDto;
public record HardwareSetResolution(AdcResolution Resolution) : ProcessingRequestDto, INotificationDto;
public record HardwareSetChannelEnabled(int ChannelIndex, bool Enabled) : ProcessingRequestDto, INotificationDto;

public abstract record HardwareSetChannelFrontendRequest(int ChannelIndex) : ProcessingRequestDto;
public record HardwareSetVoltOffset(int ChannelIndex, double VoltOffset) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetVoltFullScale(int ChannelIndex, double VoltFullScale) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetBandwidth(int ChannelIndex, ThunderscopeBandwidth Bandwidth) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetCoupling(int ChannelIndex, ThunderscopeCoupling Coupling) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetTermination(int ChannelIndex, ThunderscopeTermination Termination) : HardwareSetChannelFrontendRequest(ChannelIndex);

// Get
public record ProcessingGetStateRequest() : ProcessingRequestDto;

public record ProcessingGetModeRequest() : ProcessingRequestDto;
public record ProcessingGetDepthRequest() : ProcessingRequestDto;

public record ProcessingGetTriggerSourceRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerTypeRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerDelayRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerHoldoffRequest() : ProcessingRequestDto;
public record ProcessingGetTriggerInterpolationRequest() : ProcessingRequestDto;

public record ProcessingGetEdgeTriggerLevelRequest() : ProcessingRequestDto;
public record ProcessingGetEdgeTriggerDirectionRequest() : ProcessingRequestDto;

public record HardwareGetRateRequest() : ProcessingRequestDto;
public record HardwareGetResolutionRequest() : ProcessingRequestDto;
public record HardwareGetEnabledRequest() : ProcessingRequestDto;

public abstract record HardwareGetChannelFrontendRequest(int ChannelIndex) : ProcessingRequestDto;
public record HardwareGetVoltOffsetRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetVoltFullScaleRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetBandwidthRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetCouplingRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetTerminationRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);

// Misc
public record ProcessingGetRatesRequest() : ProcessingRequestDto;       // A collection of possible logical rates depending on hardware configuration, not an exhaustive list of all rates
public record HardwareSetChannelManualControl(int ChannelIndex, ThunderscopeChannelFrontendManualControl Channel) : ProcessingRequestDto;
public record HardwareSetAdcCalibration(ThunderscopeAdcCalibration AdcCalibration) : ProcessingRequestDto;