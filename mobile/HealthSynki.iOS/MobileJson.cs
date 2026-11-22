using System.Text.Json.Serialization;
using HealthSynki.Core;

namespace HealthSynki.iOS;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Upload))]
[JsonSerializable(typeof(Pairing))]
internal partial class MobileJson : JsonSerializerContext { }
