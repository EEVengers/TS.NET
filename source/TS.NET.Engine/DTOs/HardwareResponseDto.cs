namespace TS.NET.Engine
{
    public record HardwareResponseDto();
    public record HardwareGetRateResponse(ulong SampleRateHz) : HardwareResponseDto;
    public record HardwareGetRatesResponse(ulong[] SampleRatesHz) : HardwareResponseDto;
}
