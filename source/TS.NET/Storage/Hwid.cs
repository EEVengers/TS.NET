using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS.NET;

public class Hwid
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    //[MaxLength(256)]
    [JsonPropertyName("serial")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("boardRevision")]
    public double BoardRevision { get; set; }

    //[MaxLength(256)]
    [JsonPropertyName("buildConfiguration")]
    public string BuildConfig { get; set; } = string.Empty;

    //[MaxLength(256)]
    [JsonPropertyName("buildDate")]
    public string BuildDate { get; set; } = string.Empty;

    //[MaxLength(256)]
    [JsonPropertyName("manufacturingSignature")]
    public string ManufacturingSignature { get; set; } = string.Empty;

    private const int MaximumFieldLength = 256;

    public string ToDeviceJson()
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(SerialNumber.Length, MaximumFieldLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BuildConfig.Length, MaximumFieldLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BuildDate.Length, MaximumFieldLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ManufacturingSignature.Length, MaximumFieldLength);

        return JsonSerializer.Serialize(this, DeviceJsonSerializerContext.Default.Hwid);
    }
}
