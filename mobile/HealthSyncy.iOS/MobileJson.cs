using System.Text.Json.Serialization;
using HealthSyncy.Core;

namespace HealthSyncy.iOS;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Upload))]
[JsonSerializable(typeof(Pairing))]
internal partial class MobileJson : JsonSerializerContext { }
