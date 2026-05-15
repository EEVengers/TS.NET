using System.Text.Json.Serialization;

namespace TS.NET.Sequences;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(BenchCalibrationVariables))]
[JsonSerializable(typeof(SelfCalibrationVariables))]
[JsonSerializable(typeof(NoiseVerificationVariables))]
[JsonSerializable(typeof(BenchVerificationVariables))]
[JsonSerializable(typeof(FactoryBringUpVariables))]
internal partial class DefaultCaseContext : JsonSerializerContext { }