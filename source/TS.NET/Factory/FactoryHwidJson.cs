using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

//{
//    "$schema": "http://json-schema.org/draft-04/schema#",
//    "title": "Hardware Identity Description",
//    "type": "object",
//    "properties": {
//        "version": {
//            "type": "integer",
//            "minimum": 1
//        },
//        "Serial Number": {
//            "type": "string",
//            "maxLength": 256
//        },
//        "Board Revision": {
//            "type": "number"
//        },
//        "Build Config": {
//            "type": "string",
//            "maxLength": 256
//        },
//        "Build Date": {
//            "type": "string",
//            "maxLength": 256
//        },
//        "Mfg Signature": {
//            "type": "string",
//            "maxLength": 256
//        }
//    },
//    "required": [
//        "version",
//        "Serial Number"
//    ]
//}

namespace TS.NET
{
    public class FactoryHwidJson
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [MaxLength(256)]
        [JsonPropertyName("Serial Number")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("Board Revision")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? BoardRevision { get; set; }

        [MaxLength(256)]
        [JsonPropertyName("Build Config")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BuildConfig { get; set; }

        [MaxLength(256)]
        [JsonPropertyName("Build Date")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BuildDate { get; set; }

        [MaxLength(256)]
        [JsonPropertyName("Mfg Signature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ManufacturingSignature { get; set; }
    }
}
