using System.Text.Json;

namespace TS.NET.Sequences;

public class SelfCalibrationVariables : CalibrationVariables, IJsonVariables
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.SelfCalibrationVariables);
    }
}
