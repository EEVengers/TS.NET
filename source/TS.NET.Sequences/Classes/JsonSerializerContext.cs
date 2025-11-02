using System.Text.Json.Serialization;

namespace TS.NET.Sequences;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(BenchCalibrationVariables))]
[JsonSerializable(typeof(SelfCalibrationVariables))]
[JsonSerializable(typeof(NoiseVerificationVariables))]
[JsonSerializable(typeof(BodePlotVariables))]
internal partial class DefaultCaseContext : JsonSerializerContext { }