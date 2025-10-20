using System.Text.Json;

namespace TS.NET.Calibration;

public class SelfCalibrationVariables : CalibrationVariables, IJsonVariables
{
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.SelfCalibrationVariables);
    }
}
