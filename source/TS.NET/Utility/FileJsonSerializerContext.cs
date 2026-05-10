using System.Text.Json;
using System.Text.Json.Serialization;

namespace TS.NET;

public class CamelCaseJsonStringEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase) where TEnum : struct, Enum;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(CamelCaseJsonStringEnumConverter<PgaPreampGain>)])]
[JsonSerializable(typeof(Calibration))]
public partial class FileJsonSerializerContext : JsonSerializerContext { }