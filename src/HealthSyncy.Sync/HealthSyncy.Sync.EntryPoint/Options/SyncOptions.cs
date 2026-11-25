using System.ComponentModel.DataAnnotations;

namespace HealthSyncy.Sync.EntryPoint.Options;

public sealed record SyncOptions
{
    [Required, RegularExpression("^[0-9a-fA-F]{64}$", ErrorMessage = "{0} needs a SHA-256 digest in hex.")]
    public string UploadTokenSha256 { get; init; } = "";
}
