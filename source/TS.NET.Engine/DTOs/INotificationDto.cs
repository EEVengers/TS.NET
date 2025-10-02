using Riok.Mapperly.Abstractions;

namespace TS.NET.Engine;

public interface INotificationDto { }

[Mapper]
public static partial class NotificationMapper
{
    public static partial ProcessingConfigDto ToNotification(ThunderscopeProcessingConfig config);
}

public class ProcessingConfigDto : INotificationDto
{
    public ushort ChannelCount { get; set; }
    public int ChannelDataLength { get; set; }
    public ThunderscopeDataType ChannelDataType { get; set; }

    public TriggerChannel TriggerChannel { get; set; }
    public Mode Mode { get; set; }
    public TriggerType TriggerType { get; set; }
    public ulong TriggerDelayFs { get; set; }
    public ulong TriggerHoldoffFs { get; set; }
    public bool TriggerInterpolation { get; set; }

    public long AutoTimeoutMs { get; set; }

    //public EdgeTriggerParameters EdgeTriggerParameters;
    //public BurstTriggerParameters BurstTriggerParameters;

    //public BoxcarAveraging BoxcarAveraging;
}