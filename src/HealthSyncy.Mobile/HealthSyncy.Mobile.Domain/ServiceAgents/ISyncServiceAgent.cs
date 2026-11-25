using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Mobile.Domain.ServiceAgents;

public interface ISyncServiceAgent
{
    public Task UploadAsync(Pairing pairing, DateOnly day, UploadRequest request);
}
