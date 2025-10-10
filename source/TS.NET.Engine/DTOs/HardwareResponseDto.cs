namespace TS.NET.Engine;

public abstract record HardwareResponseDto();

public record HardwareStopResponse() : HardwareResponseDto;
public record HardwareGetRateResponse(ulong SampleRateHz) : HardwareResponseDto;
public record HardwareGetRatesResponse(ulong[] SampleRatesHz) : HardwareResponseDto;

public record HardwareGetEnabledResponse(bool Enabled) : HardwareResponseDto;
public record HardwareGetVoltOffsetResponse(double RequestedVoltOffset, double ActualVoltOffset) : HardwareResponseDto;
public record HardwareGetVoltFullScaleResponse(double RequestedVoltFullScale, double ActualVoltFullScale) : HardwareResponseDto;
public record HardwareGetBandwidthResponse(ThunderscopeBandwidth Bandwidth) : HardwareResponseDto;
public record HardwareGetCouplingResponse(ThunderscopeCoupling Coupling) : HardwareResponseDto;
public record HardwareGetTerminationResponse(ThunderscopeTermination Termination) : HardwareResponseDto;
