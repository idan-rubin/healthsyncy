using System.ComponentModel.DataAnnotations;
using HealthSyncy.Common.Infrastructure.Validation;

namespace HealthSyncy.Connector.EntryPoint.Options;

public sealed record ConnectorOptions
{
    [Required, HttpsUrl] public string OAuthIssuer { get; init; } = "";
    [Required, HttpsUrl] public string McpResource { get; init; } = "";
    [Required] public string OwnerSubject { get; init; } = "";
}
