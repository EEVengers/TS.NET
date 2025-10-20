using System.Text.Json.Serialization;
using TS.NET.Calibration;

namespace TS.NET.Testbench.UI;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(VariablesDto))]
[JsonSerializable(typeof(VariablesFile))]
internal partial class DefaultCaseContext : JsonSerializerContext { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(StepDto))]
[JsonSerializable(typeof(StepResultDto))]
[JsonSerializable(typeof(ChannelPathConfig))]
[JsonSerializable(typeof(LogDto))]
//[JsonSerializable(typeof(VariablesDto))]
[JsonSerializable(typeof(SequenceDto))]
[JsonSerializable(typeof(StepUpdateDto))]
[JsonSerializable(typeof(LogUpdateDto))]
[JsonSerializable(typeof(SequenceStatusUpdateDto))]
[JsonSerializable(typeof(string))]
internal partial class CamelCaseContext : JsonSerializerContext { }
