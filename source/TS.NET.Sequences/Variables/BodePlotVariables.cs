using System.Text.Json;

namespace TS.NET.Sequences;

public class BodePlotVariables : BenchCalibrationVariables, IJsonVariables
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.BodePlotVariables);
    }
}
