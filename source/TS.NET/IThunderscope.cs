namespace TS.NET
{
    public interface IThunderscope
    {
        void Start();
        void Stop();
        ThunderscopeChannelFrontend GetChannelFrontend(int channelIndex);
        ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex);
        void SetChannelEnable(int channelIndex, bool enabled);
        void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel);
        void SetChannelCalibration(int channelIndex, ThunderscopeChannelCalibration channelCalibration);
        void Read(ThunderscopeMemory data);
        bool TryRead(ThunderscopeMemory data);
        ThunderscopeHardwareConfig GetConfiguration();
        void SetRate(ulong sampleRateHz);
        void SetResolution(AdcResolution resolution);
    }
}
