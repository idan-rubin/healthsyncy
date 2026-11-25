using System.ComponentModel.DataAnnotations;

namespace HealthSyncy.Common.Infrastructure.Repository;

public sealed record MongoOptions
{
    [Required] public string MongoUri { get; init; } = "";
    [Required] public string MongoDatabase { get; init; } = "activity";
}
