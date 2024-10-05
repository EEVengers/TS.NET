using System;

namespace TS.NET.Engine
{
    public record HardwareResponseDto();
    public record HardwareGetRatesResponse(ulong SampleRateHz) : HardwareResponseDto;
}
