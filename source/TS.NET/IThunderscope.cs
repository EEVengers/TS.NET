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
        bool TryRead(Span<byte> data, out ulong sampleStartIndex, out int sampleLengthPerChannel);
        ThunderscopeHardwareConfig GetConfiguration();
        void SetRate(ulong sampleRateHz);
        void SetResolution(AdcResolution resolution);
        bool TryGetEvent(out ThunderscopeEvent thunderscopeEvent, out ulong eventSampleIndex);
        void AssertEvent();
        void SetPeriodicEventSync(uint period_us);
        void SetExtSyncMode(ThunderscopeExtSyncMode externalSyncMode);
        void SetRefClockMode(ThunderscopeRefClockMode refClockMode);
        void SetRefClockFrequency(uint frequencyHz);
    }

    public enum ThunderscopeEvent : byte
    {
        None = 0,
        SyncOutputRisingEdge = 1,
        SyncInputRisingEdge = 2
    }

    public enum ThunderscopeExtSyncMode : byte
    {
        Disabled = 0,
        Output = 1,
        Input = 2
    }

    public enum ThunderscopeRefClockMode : byte
    {
        Disabled = 0,
        Output = 1,
        Input = 2
    }
}
