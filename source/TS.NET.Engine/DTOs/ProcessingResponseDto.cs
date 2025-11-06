namespace TS.NET.Engine;

public abstract record ProcessingResponseDto();

//public record ProcessingGetRateResponseDto(uint SampleRate) : ProcessingResponseDto();
public record ProcessingGetStateResponse(bool Run) : ProcessingResponseDto();

public record ProcessingGetRatesResponse(ulong[] SampleRatesHz) : ProcessingResponseDto;

public record ProcessingGetModeResponse(Mode Mode) : ProcessingResponseDto();
public record ProcessingGetDepthResponse(int Depth) : ProcessingResponseDto();
public record ProcessingGetRateResponse(ulong SampleRateHz) : ProcessingResponseDto;
public record ProcessingGetResolutionResponse(AdcResolution Resolution) : ProcessingResponseDto();

// Trigger query responses
public record ProcessingGetTriggerSourceResponse(TriggerChannel Channel) : ProcessingResponseDto();
public record ProcessingGetTriggerTypeResponse(TriggerType Type) : ProcessingResponseDto();
public record ProcessingGetTriggerDelayResponse(ulong Femtoseconds) : ProcessingResponseDto();
public record ProcessingGetTriggerHoldoffResponse(ulong Femtoseconds) : ProcessingResponseDto();
public record ProcessingGetTriggerInterpolationResponse(bool Enabled) : ProcessingResponseDto();

public record ProcessingGetEdgeTriggerLevelResponse(double LevelVolts) : ProcessingResponseDto();
public record ProcessingGetEdgeTriggerDirectionResponse(EdgeDirection Direction) : ProcessingResponseDto();
