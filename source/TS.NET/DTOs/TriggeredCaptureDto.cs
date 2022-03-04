using MessagePack;

namespace TS.NET;

[MessagePackObject]
public struct TriggeredCaptureDto
{
    [Key(0)]
    public Channels Channels { get; set; }
    [Key(1)]
    public int ChannelLength { get; set; }
    [Key(2)]
    public Memory<byte> ChannelData { get; set; }
}