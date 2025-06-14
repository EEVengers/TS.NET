﻿using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace TS.NET.Engine
{
    [YamlSerializable]
    public class ThunderscopeSettings
    {
        public string HardwareDriver { get; set; } = "";
        public string HardwareRevision { get; set; } = "";
        public int MaxCaptureLength { get; set; }
        public int ScpiPort { get; set; }
        public int DataPort { get; set; }
        public bool DataPortEnabled { get; set; }

        public int HardwareThreadProcessorAffinity { get; set; }
        public int ProcessingThreadProcessorAffinity { get; set; }

        public ThunderscopeCalibrationSettings XdmaCalibration { get; set; } = new();
        public ThunderscopeCalibrationSettings LiteXCalibration { get; set; } = new();

        public static ThunderscopeSettings Default()
        {
            return new ThunderscopeSettings()
            {
                HardwareDriver = "XDMA",
                HardwareRevision = "Rev4",
                MaxCaptureLength = 10000000,
                ScpiPort = 5025,
                DataPort = 5026,
                DataPortEnabled = true,

                HardwareThreadProcessorAffinity = -1,
                ProcessingThreadProcessorAffinity = -1,

                XdmaCalibration = ThunderscopeCalibrationSettings.Default(),
                LiteXCalibration = ThunderscopeCalibrationSettings.Default()
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
