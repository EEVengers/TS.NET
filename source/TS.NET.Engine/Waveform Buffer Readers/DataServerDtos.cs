namespace TS.NET.Engine;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WaveformHeaderOld
{
    public uint seqnum;
    public ushort numChannels;
    public ulong fsPerSample;
    public long triggerFs;
    public double hwWaveformsPerSec;

    public override string ToString()
    {
        return $"seqnum: {seqnum}, numChannels: {numChannels}, fsPerSample: {fsPerSample}, triggerFs: {triggerFs}, hwWaveformsPerSec: {hwWaveformsPerSec}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WaveformHeader
{
    public byte version;      // Starts from 1
    public uint seqnum;
    public ushort numChannels;
    public ulong fsPerSample;
    public long triggerFs;
    public double hwWaveformsPerSec;

    public override string ToString()
    {
        return $"seqnum: {seqnum}, numChannels: {numChannels}, fsPerSample: {fsPerSample}, triggerFs: {triggerFs}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChannelHeaderOld
{
    public byte channelIndex;
    public ulong depth;
    public float scale;
    public float offset;
    public float trigphase;
    public byte clipping;
    public override string ToString()
    {
        return $"chNum: {channelIndex}, depth: {depth}, scale: {scale}, offset: {offset}, trigphase: {trigphase}, clipping: {clipping}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChannelHeader
{
    public byte channelIndex;
    public ulong depth;
    public float scale;
    public float offset;
    public float trigphase;
    public byte clipping;
    public byte dataType;             // ThunderscopeDataType, I8 = 2, I16 = 4
    public override string ToString()
    {
        return $"chNum: {channelIndex}, depth: {depth}, scale: {scale}, offset: {offset}, trigphase: {trigphase}";
    }
}
