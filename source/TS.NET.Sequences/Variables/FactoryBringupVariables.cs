using System.Text.Json;
using TS.NET;
using TS.NET.Sequences;

public class FactoryBringUpVariables : CalibrationVariables, IJsonVariables
{
    public string? SigGen1Host { get; set; }
    public string? SigGen2Host { get; set; }

    private double sigGenZero = 0;
    public double SigGenZero { get => sigGenZero; set => sigGenZero = Math.Round((double)value, 6); }

    public string? FpgaModel { get; set; }
    public ulong? FpgaDna { get; set; }
    public string? FpgaFlashImagePath { get; set; }

    public Hwid Hwid { get; set; } = new();

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.FactoryBringUpVariables);
    }
}