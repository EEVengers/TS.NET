using System.Text.Json;

namespace TS.NET.Sequences;

public class NoiseVerificationVariables : CommonVariables, IJsonVariables
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.NoiseVerificationVariables);
    }
}
