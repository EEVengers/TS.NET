using System;

namespace TS.NET.Engine
{
    public abstract record HardwareRequestDto();
    public record HardwareStartRequest() : HardwareRequestDto;
    public record HardwareStopRequest() : HardwareRequestDto;

    public abstract record HardwareSetChannelFrontendRequestDto(int ChannelIndex) : HardwareRequestDto;
    public record HardwareSetEnabledRequest(int ChannelIndex, bool Enabled) : HardwareSetChannelFrontendRequestDto(ChannelIndex);
    public record HardwareSetVoltOffsetRequest(int ChannelIndex, double VoltOffset) : HardwareSetChannelFrontendRequestDto(ChannelIndex);
    public record HardwareSetVoltFullScaleRequest(int ChannelIndex, double VoltFullScale) : HardwareSetChannelFrontendRequestDto(ChannelIndex);
    public record HardwareSetBandwidthRequest(int ChannelIndex, ThunderscopeBandwidth Bandwidth) : HardwareSetChannelFrontendRequestDto(ChannelIndex);
    public record HardwareSetCouplingRequest(int ChannelIndex, ThunderscopeCoupling Coupling) : HardwareSetChannelFrontendRequestDto(ChannelIndex);
    public record HardwareSetTerminationRequest(int ChannelIndex, ThunderscopeTermination Termination) : HardwareSetChannelFrontendRequestDto(ChannelIndex);

    public abstract record HardwareSetChannelCalibrationRequestDto(int ChannelIndex) : HardwareRequestDto;
    public record HardwareSetOffsetVoltageLowGainRequest(int ChannelIndex, double OffsetVoltage) : HardwareSetChannelCalibrationRequestDto(ChannelIndex);
}