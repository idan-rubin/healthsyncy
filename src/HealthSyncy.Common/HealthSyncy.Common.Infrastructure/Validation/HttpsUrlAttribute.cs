using System.ComponentModel.DataAnnotations;

namespace HealthSyncy.Common.Infrastructure.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class HttpsUrlAttribute() : ValidationAttribute("{0} needs an HTTPS URL.")
{
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string text => Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri is { Scheme: "https", UserInfo.Length: 0, Query.Length: 0, Fragment.Length: 0 },
        _ => false
    };
}
