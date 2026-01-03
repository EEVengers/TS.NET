namespace TS.NET.Engine;

public abstract record ProcessingResponseDto();

//public record ProcessingGetRateResponseDto(uint SampleRate) : ProcessingResponseDto();
public record ProcessingGetStateResponse(bool Run) : ProcessingResponseDto();

public record ProcessingGetRatesResponse(ulong[] SampleRatesHz) : ProcessingResponseDto;

public record ProcessingGetModeResponse(Mode Mode) : ProcessingResponseDto();
public record ProcessingGetDepthResponse(int Depth) : ProcessingResponseDto();
public record HardwareGetRateResponse(ulong SampleRateHz) : ProcessingResponseDto;
public record HardwareGetResolutionResponse(AdcResolution Resolution) : ProcessingResponseDto();

public record HardwareGetEnabledResponse(byte EnabledChannels) : ProcessingResponseDto();

// Trigger query responses
public record ProcessingGetTriggerSourceResponse(TriggerChannel Channel) : ProcessingResponseDto();
public record ProcessingGetTriggerTypeResponse(TriggerType Type) : ProcessingResponseDto();
public record ProcessingGetTriggerDelayResponse(ulong Femtoseconds) : ProcessingResponseDto();
public record ProcessingGetTriggerHoldoffResponse(ulong Femtoseconds) : ProcessingResponseDto();
public record ProcessingGetTriggerInterpolationResponse(bool Enabled) : ProcessingResponseDto();

public record ProcessingGetEdgeTriggerLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetEdgeTriggerDirectionResponse(EdgeDirection Direction) : ProcessingResponseDto();

public record HardwareGetVoltOffsetResponse(double RequestedVoltOffset, double ActualVoltOffset) : ProcessingResponseDto;
public record HardwareGetVoltFullScaleResponse(double RequestedVoltFullScale, double ActualVoltFullScale) : ProcessingResponseDto;
public record HardwareGetBandwidthResponse(ThunderscopeBandwidth Bandwidth) : ProcessingResponseDto;
public record HardwareGetCouplingResponse(ThunderscopeCoupling Coupling) : ProcessingResponseDto;
public record HardwareGetTerminationResponse(ThunderscopeTermination RequestedTermination, ThunderscopeTermination ActualTermination) : ProcessingResponseDto;