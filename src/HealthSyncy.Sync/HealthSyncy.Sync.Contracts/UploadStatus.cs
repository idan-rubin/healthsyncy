using System.Text.Json.Serialization;

namespace HealthSyncy.Sync.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<UploadStatus>))]
public enum UploadStatus
{
    Saved,
    Duplicate,
    Superseded
}
