using System.Text.Json;

namespace TS.NET.Sequences;

public class BenchCalibrationVariables : CalibrationVariables, IJsonVariables
{
    public string? SigGen1Host { get; set; }
    public string? SigGen2Host { get; set; }

    private double sigGenZero = 0;
    public double SigGenZero { get => sigGenZero; set => sigGenZero = Math.Round((double)value, 6); }

    private double referenceVpp = 0;
    public double ReferenceVpp { get => referenceVpp; set => referenceVpp = Math.Round((double)value, 6); }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.BenchCalibrationVariables);
    }
}
