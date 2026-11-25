using HealthSyncy.Core;

namespace HealthSyncy.BL;

public enum UploadResult { Saved, Duplicate, Superseded, Conflict, Invalid, Paused }

public interface IUploadBL
{
    Task<UploadResult> Accept(string day, Upload upload, CancellationToken ct);
}
