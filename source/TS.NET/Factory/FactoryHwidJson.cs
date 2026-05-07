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
        [JsonPropertyName("serial")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("boardRevision")]
        public double BoardRevision { get; set; }

        [MaxLength(256)]
        [JsonPropertyName("buildConfiguration")]
        public string BuildConfig { get; set; } = string.Empty;

        [MaxLength(256)]
        [JsonPropertyName("buildDate")]
        public string BuildDate { get; set; } = string.Empty;

        [MaxLength(256)]
        [JsonPropertyName("manufacturingSignature")]
        public string ManufacturingSignature { get; set; } = string.Empty;
    }
}
