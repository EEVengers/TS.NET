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
public record ProcessingGetEdgeTriggerHysteresisResponse(double Percent) : ProcessingResponseDto();
public record ProcessingGetWindowTriggerUpperLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetWindowTriggerLowerLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetWindowTriggerDirectionResponse(WindowDirection Direction) : ProcessingResponseDto();
public record ProcessingGetBurstTriggerLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetBurstTriggerDirectionResponse(BurstEdgeDirection Direction) : ProcessingResponseDto();
public record ProcessingGetBurstTriggerHysteresisResponse(double Percent) : ProcessingResponseDto();
public record ProcessingGetBurstTriggerQuietUpperLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetBurstTriggerQuietLowerLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetBurstTriggerQuietTimeResponse(long Femtoseconds) : ProcessingResponseDto();

public record HardwareGetVoltOffsetResponse(double RequestedVoltOffset, double ActualVoltOffset) : ProcessingResponseDto;
public record HardwareGetVoltFullScaleResponse(double RequestedVoltFullScale, double ActualVoltFullScale) : ProcessingResponseDto;
public record HardwareGetBandwidthResponse(ThunderscopeBandwidth Bandwidth) : ProcessingResponseDto;
public record HardwareGetCouplingResponse(ThunderscopeCoupling Coupling) : ProcessingResponseDto;
public record HardwareGetTerminationResponse(ThunderscopeTermination RequestedTermination, ThunderscopeTermination ActualTermination) : ProcessingResponseDto;

public record HardwareGetTemperatureResponse(float Temperature) : ProcessingResponseDto;