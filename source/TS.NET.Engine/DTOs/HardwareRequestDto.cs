using System;

namespace TS.NET.Engine
{
    public abstract record HardwareRequestDto();
    public record HardwareStartRequest() : HardwareRequestDto;
    public record HardwareStopRequest() : HardwareRequestDto;

    public abstract record HardwareConfigureChannelDto(int Channel): HardwareRequestDto;
    public record HardwareSetEnabledRequest(int Channel, bool Enabled) : HardwareConfigureChannelDto(Channel);
    public record HardwareSetVoltOffsetRequest(int Channel, double VoltOffset) : HardwareConfigureChannelDto(Channel);
    public record HardwareSetVoltFullScaleRequest(int Channel, double VoltFullScale) : HardwareConfigureChannelDto(Channel);
    public record HardwareSetBandwidthRequest(int Channel, int Bandwidth) : HardwareConfigureChannelDto(Channel);
    public record HardwareSetCouplingRequest(int Channel, ThunderscopeCoupling Coupling) : HardwareConfigureChannelDto(Channel);
    public record HardwareSetTerminationRequest(int Channel, ThunderscopeTermination Termination) : HardwareConfigureChannelDto(Channel);
}