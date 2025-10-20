using System.Text.Json;
using System.Text.Json.Serialization;
using TS.NET.Calibration;

namespace TS.NET.Testbench.UI;

public class VariablesDto : MessageDto
{
    [JsonPropertyName("variables")]
    public required JsonElement Variables { get; set; }

    internal static VariablesDto FromVariables(IJsonVariables variables)
    {
        var jsonString = variables.ToJson();
        var jsonElement = JsonDocument.Parse(jsonString).RootElement;
        return new VariablesDto { Type = "variables", Variables = jsonElement };
    }
}
