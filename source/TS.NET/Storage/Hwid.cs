using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS.NET;

public class Hwid
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [MaxLength(256)]
    [JsonPropertyName("serial")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("boardRevision")]
    public double BoardRevision { get; set; }

    [MaxLength(256)]
    [JsonPropertyName("buildConfiguration")]
    public string BuildConfig { get; set; } = string.Empty;

    [MaxLength(256)]
    [JsonPropertyName("buildDate")]
    public string BuildDate { get; set; } = string.Empty;

    [MaxLength(256)]
    [JsonPropertyName("manufacturingSignature")]
    public string ManufacturingSignature { get; set; } = string.Empty;

    public string ToDeviceJson()
    {
        return JsonSerializer.Serialize(this, DeviceJsonSerializerContext.Default.Hwid);
    }
}
