using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace TS.NET.Engine;

[YamlSerializable]
public class ThunderscopeSettings
{
    public string HardwareDriver { get; set; } = "";
    public string HardwareRevision { get; set; } = "";
    public int MaxCaptureLength { get; set; }
    public int ScpiPort { get; set; }
    public int DataPort { get; set; }
    public string WaveformBufferReader { get; set; } = "";

    public int ProcessingThreadProcessorAffinity { get; set; } = -1;
    
    public const int SegmentLengthBytes = 8 * 1024 * 1024;

    public static ThunderscopeSettings Default()
    {
        return new ThunderscopeSettings()
        {
            HardwareDriver = "LiteX",
            HardwareRevision = "Rev5",
            MaxCaptureLength = 10000000,
            ScpiPort = 5025,
            DataPort = 5026,
            WaveformBufferReader = "DataServer",

            ProcessingThreadProcessorAffinity = -1,
        };
    }

    public static ThunderscopeSettings FromJsonFile(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException(file);

        return JsonSerializer.Deserialize(File.ReadAllText(file), SourceGenerationContext.Default.ThunderscopeSettings) ?? throw new ArgumentNullException();
    }

    public static ThunderscopeSettings FromYamlFile(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException(file);

        var context = new StaticContext();
        var deserializer = new StaticDeserializerBuilder(context)
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<ThunderscopeSettings>(File.ReadAllText(file));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ThunderscopeSettings))]
[JsonSerializable(typeof(ThunderscopeCalibrationSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

[YamlStaticContext]
public partial class StaticContext : YamlDotNet.Serialization.StaticContext { }
