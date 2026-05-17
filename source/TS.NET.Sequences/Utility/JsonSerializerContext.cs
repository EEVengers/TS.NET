using System.Text.Json.Serialization;

namespace TS.NET.Sequences;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(FactoryBringUpVariables))]
[JsonSerializable(typeof(FactoryVariables))]
[JsonSerializable(typeof(SelfCalibrationVariables))]
[JsonSerializable(typeof(NoiseVerificationVariables))]
internal partial class DefaultCaseContext : JsonSerializerContext { }