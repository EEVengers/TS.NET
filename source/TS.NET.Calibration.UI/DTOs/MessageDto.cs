using System.Text.Json.Serialization;

namespace TS.NET.Calibration.UI;

public class MessageDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}