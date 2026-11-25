using Microsoft.Extensions.Configuration;

namespace HealthSyncy.Core;

public static class Configuration
{
    public static string Required(this IConfiguration config, string name) =>
        !string.IsNullOrWhiteSpace(config[name]) ? config[name]! :
            throw new InvalidOperationException($"Missing setting {name}.");

    public static string Https(this IConfiguration config, string name)
    {
        var value = config.Required(name);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException($"{name} needs an HTTPS URL.");
        return value;
    }
}
