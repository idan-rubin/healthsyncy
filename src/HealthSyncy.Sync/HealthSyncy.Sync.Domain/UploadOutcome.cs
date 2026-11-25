namespace HealthSyncy.Sync.Domain;

public readonly union UploadOutcome(Accepted, Rejected, Paused, Conflicting);
