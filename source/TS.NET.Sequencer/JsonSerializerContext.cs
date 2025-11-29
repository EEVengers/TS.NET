using System.Text.Json.Serialization;

namespace TS.NET.Sequencer;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(ResultMetadataXYChart))]
internal partial class DefaultCaseContext : JsonSerializerContext { }