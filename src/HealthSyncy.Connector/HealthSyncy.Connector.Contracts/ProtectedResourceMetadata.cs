using System.Text.Json.Serialization;

namespace HealthSyncy.Connector.Contracts;

public sealed record ProtectedResourceMetadata(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("authorization_servers")] IReadOnlyList<string> AuthorizationServers,
    [property: JsonPropertyName("scopes_supported")] IReadOnlyList<string> ScopesSupported,
    [property: JsonPropertyName("bearer_methods_supported")] IReadOnlyList<string> BearerMethodsSupported)
{
    public const string Path = "/.well-known/oauth-protected-resource/mcp";
    public const string Scope = "activity.read";
}
