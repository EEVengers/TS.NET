using System.Text.Json;

namespace TS.NET.Sequences;

public class BenchVerificationVariables : BenchCalibrationVariables, IJsonVariables
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.BenchVerificationVariables);
    }
}
