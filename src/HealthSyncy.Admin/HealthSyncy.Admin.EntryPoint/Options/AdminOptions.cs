using System.ComponentModel.DataAnnotations;
using HealthSyncy.Common.Infrastructure.Validation;

namespace HealthSyncy.Admin.EntryPoint.Options;

public sealed record AdminOptions
{
    [Required, HttpsUrl] public string OAuthIssuer { get; init; } = "";
    [Required, HttpsUrl] public string AdminOrigin { get; init; } = "";
    [Required] public string AdminSubject { get; init; } = "";
    [Required] public string AdminClientId { get; init; } = "";
    [Required] public string AdminClientSecret { get; init; } = "";
    [Required] public string KeyDirectory { get; init; } = "/var/lib/healthsyncy/keys";
    [Required, IanaTimeZone] public string ReportingZone { get; init; } = "UTC";

    public string SignInCallback => AdminOrigin.TrimEnd('/') + "/signin-oidc";
}
