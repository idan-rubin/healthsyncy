using System.ComponentModel.DataAnnotations;
using HealthSyncy.Common.Infrastructure.Validation;

namespace HealthSyncy.Common.Infrastructure.Repository;

public sealed record MongoOptions
{
    [Required, MongoConnectionString] public string MongoUri { get; init; } = "";
    [Required] public string MongoDatabase { get; init; } = "activity";
}
