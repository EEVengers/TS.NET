using System.Text.Json.Serialization;
using TS.NET.Calibration;

namespace TS.NET.Testbench.UI;

public class VariablesDto : MessageDto
{
    [JsonPropertyName("variables")]
    public required Variables Variables { get; set; }

    internal static VariablesDto FromVariables()
    {
        return new VariablesDto { Type = "variables", Variables = Variables.Instance };
    }
}
