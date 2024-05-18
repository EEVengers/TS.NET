using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace TS.NET.Engine
{
    public class ThunderscopeSettings
    {
        public required string Driver { get; set; }
        public required ushort MaxChannelCount { get; set; }
        public required uint MaxChannelDataLength { get; set; }
        public required int InputThreadProcessorAffinity { get; set; }
        public required int ProcessingThreadProcessorAffinity { get; set; }
        public required ThunderscopeCalibration Calibration { get; set; }

        public static ThunderscopeSettings Default()
        {
            return new ThunderscopeSettings()
            {
                Driver = "XDMA",
                MaxChannelCount = 4,
                MaxChannelDataLength = 1000000,
                InputThreadProcessorAffinity = -1,
                ProcessingThreadProcessorAffinity = -1,
                Calibration = ThunderscopeCalibration.Default()
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

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            return deserializer.Deserialize<ThunderscopeSettings>(File.ReadAllText(file));
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ThunderscopeSettings))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
}
