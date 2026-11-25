using System.Text.Json.Serialization;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Mobile.Domain;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UploadRequest))]
[JsonSerializable(typeof(Pairing))]
public sealed partial class MobileJson : JsonSerializerContext;
