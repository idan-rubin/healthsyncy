using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Sync.Domain;

public interface IUploadService
{
    public Task<UploadOutcome> AcceptAsync(
        string day,
        UploadRequest request,
        CancellationToken cancellationToken = default);
}
