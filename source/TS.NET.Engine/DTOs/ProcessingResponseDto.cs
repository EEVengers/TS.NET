namespace TS.NET.Engine
{
    public record ProcessingResponseDto();

    //public record ProcessingGetRateResponseDto(uint SampleRate) : ProcessingResponseDto();
    public record ProcessingGetModeResponse(Mode Mode): ProcessingResponseDto();
}
