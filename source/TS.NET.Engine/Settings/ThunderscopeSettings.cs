using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TS.NET.Engine;

[YamlSerializable]
public class ThunderscopeSettings
{
    public int ConfigurationVersion { get; set; } = 1;
    public string HardwareDriver { get; set; } = "";
    public string HardwareRevision { get; set; } = "";
    public int MaxCaptureLength { get; set; }
    public string ScpiServer { get; set; } = "";
    public string DataServer { get; set; } = "";
    public string WaveformBufferReader { get; set; } = "";

    public int ProcessingThreadProcessorAffinity { get; set; } = -1;

    public const int SegmentLengthBytes = 8 * 1024 * 1024;

    public static ThunderscopeSettings Default()
    {
        return new ThunderscopeSettings()
        {
            ConfigurationVersion = 1,
            HardwareDriver = "LiteX",
            HardwareRevision = "Rev5",
            MaxCaptureLength = 10000000,
            ScpiServer = "127.0.0.1:5025",
            DataServer = "127.0.0.1:5026",
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

        return FromYaml(File.ReadAllText(file));
    }

    internal static ThunderscopeSettings FromYaml(string yaml)
    {
        var context = new StaticContext();
        var deserializer = new StaticDeserializerBuilder(context)
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<ThunderscopeSettings>(yaml);
    }

}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ThunderscopeSettings))]
[JsonSerializable(typeof(Calibration))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

[YamlStaticContext]
public partial class StaticContext : YamlDotNet.Serialization.StaticContext { }
