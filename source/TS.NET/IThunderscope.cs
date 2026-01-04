namespace TS.NET
{
    public interface IThunderscope
    {
        void Start();
        void Stop();
        bool Running();
        ThunderscopeChannelFrontend GetChannelFrontend(int channelIndex);
        ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex);
        void SetChannelEnable(int channelIndex, bool enabled);
        void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel);
        void SetChannelCalibration(int channelIndex, ThunderscopeChannelCalibration channelCalibration);
        void Read(ThunderscopeMemory data);
        bool TryRead(ThunderscopeMemory data, out ulong sampleStartIndex, out int sampleLengthPerChannel);
        bool TryGetEvent(out ThunderscopeEvent thunderscopeEvent, out ulong eventSampleIndex);
        ThunderscopeHardwareConfig GetConfiguration();
        void SetRate(ulong sampleRateHz);
        void SetResolution(AdcResolution resolution);
        void SetExternalSync(ThunderscopeExternalSync externalSync);
    }

    public enum ThunderscopeEvent : byte
    {
        SyncInputRisingEdge = 1
    }

    public enum ThunderscopeExternalSync : byte
    {
        Disabled = 0,
        Output = 1,
        Input = 2
    }
}
