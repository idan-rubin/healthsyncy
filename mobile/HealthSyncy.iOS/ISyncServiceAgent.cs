using HealthSyncy.Core;

namespace HealthSyncy.iOS;

internal interface ISyncServiceAgent
{
    Task Upload(Pairing pairing, DateOnly day, Upload upload);
}
