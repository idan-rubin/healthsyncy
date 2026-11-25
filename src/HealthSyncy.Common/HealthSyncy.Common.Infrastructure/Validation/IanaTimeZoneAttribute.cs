using System.ComponentModel.DataAnnotations;
using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Common.Infrastructure.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IanaTimeZoneAttribute() : ValidationAttribute("{0} needs an IANA time zone, such as America/New_York.")
{
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string id => Reporting.IsZone(id),
        _ => false
    };
}
