namespace TS.NET
{
    public interface IThunderscope
    {
        void Start();
        void Stop();
        ThunderscopeChannel GetChannel(int channelIndex);
        void SetChannel(int channelIndex, ThunderscopeChannel channel);
        void SetChannelEnable(int channelIndex, bool enabled);
        void Read(ThunderscopeMemory data, CancellationToken cancellationToken);
        ThunderscopeHardwareConfig GetConfiguration();
    }
}
