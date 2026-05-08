using System.Text.Json.Serialization;

namespace TS.NET
{
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        Converters = [typeof(CamelCaseJsonStringEnumConverter<PgaPreampGain>)])]
    [JsonSerializable(typeof(FactoryHwidJson))]
    [JsonSerializable(typeof(ThunderscopeCalibrationSettings))]
    public partial class DeviceJsonSerializerContext : JsonSerializerContext { }
}
