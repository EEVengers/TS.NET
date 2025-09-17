using System.Text.Json.Serialization;

namespace TS.NET.Calibration.UI
{
    public class VariablesDto : MessageDto
    {
        [JsonPropertyName("variables")]
        public required Variables Variables { get; set; }

        internal static VariablesDto FromVariables()
        {
            return new VariablesDto { Type = "variables", Variables = Variables.Instance };
        }
    }
}
