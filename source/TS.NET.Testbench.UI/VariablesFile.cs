using System.Text.Json;

namespace TS.NET.Testbench.UI;

public class VariablesFile
{
    public required string SigGen1Ip { get; set; }
    public required string SigGen2Ip { get; set; }
    public required bool Factory { get; set; }

    public static VariablesFile FromJsonFile(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException(file);

        return JsonSerializer.Deserialize(File.ReadAllText(file), DefaultCaseContext.Default.VariablesFile) ?? throw new ArgumentNullException();
    }
}
