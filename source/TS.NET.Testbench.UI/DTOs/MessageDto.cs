using System.Text.Json.Serialization;

namespace TS.NET.Testbench.UI;

public class MessageDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}