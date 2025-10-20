using System.Text.Json.Serialization;

namespace TS.NET.Calibration;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(BenchCalibrationVariables))]
[JsonSerializable(typeof(SelfCalibrationVariables))]
internal partial class DefaultCaseContext : JsonSerializerContext { }