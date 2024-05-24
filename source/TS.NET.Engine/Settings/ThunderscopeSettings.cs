using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace TS.NET.Engine
{
    [YamlSerializable]
    public class ThunderscopeSettings
    {
        public string Driver { get; set; }
        public ushort MaxChannelCount { get; set; }
        public uint MaxChannelDataLength { get; set; }
        public bool Twinlan { get; set; }

        public int HardwareThreadProcessorAffinity { get; set; }
        public int ProcessingThreadProcessorAffinity { get; set; }

        public ThunderscopeCalibrationSettings Calibration { get; set; }

        public static ThunderscopeSettings Default()
        {
            return new ThunderscopeSettings()
            {
                Driver = "XDMA",
                MaxChannelCount = 4,
                MaxChannelDataLength = 1000000,
                Twinlan = true,
                HardwareThreadProcessorAffinity = -1,
                ProcessingThreadProcessorAffinity = -1,
                Calibration = ThunderscopeCalibrationSettings.Default()
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
                .Build();

            return deserializer.Deserialize<ThunderscopeSettings>(File.ReadAllText(file));
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ThunderscopeSettings))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }

    [YamlStaticContext]
    public partial class StaticContext : YamlDotNet.Serialization.StaticContext { }
}
