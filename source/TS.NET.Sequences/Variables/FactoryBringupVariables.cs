using System.Text.Json;
using TS.NET;
using TS.NET.Sequences;

public class FactoryBringUpVariables : FactoryVariables, IJsonVariables
{
    public string? FpgaModel { get; set; }
    public ulong? FpgaDna { get; set; }
    public string? FpgaFlashImagePath { get; set; }

    public Hwid Hwid { get; set; } = new();

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DefaultCaseContext.Default.FactoryBringUpVariables);
    }
}