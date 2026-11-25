using System.ComponentModel.DataAnnotations;
using MongoDB.Driver;

namespace HealthSyncy.Common.Infrastructure.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MongoConnectionStringAttribute() : ValidationAttribute("{0} needs a MongoDB connection string.")
{
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string text => IsConnectionString(text),
        _ => false
    };

    private static bool IsConnectionString(string text)
    {
        try
        {
            _ = MongoUrl.Create(text);
            return true;
        }
        catch (MongoConfigurationException)
        {
            return false;
        }
    }
}
