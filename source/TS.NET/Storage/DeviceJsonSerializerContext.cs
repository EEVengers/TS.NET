using System.Text.Json.Serialization;

namespace TS.NET;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(CamelCaseJsonStringEnumConverter<PgaPreampGain>)])]
[JsonSerializable(typeof(Hwid))]
[JsonSerializable(typeof(Calibration))]
public partial class DeviceJsonSerializerContext : JsonSerializerContext { }
