namespace TS.NET.Engine;

public abstract record HardwareRequestDto();

// Only DTOs that generate a response, have "Request" in the name. 

// Control
public record HardwareStart() : HardwareRequestDto;
public record HardwareStopRequest() : HardwareRequestDto;

// Set
public record HardwareSetRate(ulong Rate) : HardwareRequestDto;
public record HardwareSetResolution(AdcResolution Resolution) : HardwareRequestDto;

public abstract record HardwareSetChannelFrontendRequest(int ChannelIndex) : HardwareRequestDto;
public record HardwareSetEnabled(int ChannelIndex, bool Enabled) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetVoltOffset(int ChannelIndex, double VoltOffset) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetVoltFullScale(int ChannelIndex, double VoltFullScale) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetBandwidth(int ChannelIndex, ThunderscopeBandwidth Bandwidth) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetCoupling(int ChannelIndex, ThunderscopeCoupling Coupling) : HardwareSetChannelFrontendRequest(ChannelIndex);
public record HardwareSetTermination(int ChannelIndex, ThunderscopeTermination Termination) : HardwareSetChannelFrontendRequest(ChannelIndex);

public record HardwareSetChannelManualControl(int ChannelIndex, ThunderscopeChannelFrontendManualControl Channel) : HardwareRequestDto;
public record HardwareSetAdcCalibration(ThunderscopeAdcCalibration AdcCalibration) : HardwareRequestDto;

// Get
public abstract record HardwareGetChannelFrontendRequest(int ChannelIndex) : HardwareRequestDto;
public record HardwareGetVoltOffsetRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetVoltFullScaleRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetBandwidthRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetCouplingRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);
public record HardwareGetTerminationRequest(int ChannelIndex) : HardwareGetChannelFrontendRequest(ChannelIndex);