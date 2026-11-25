namespace HealthSyncy.Mobile.Domain.Services;

public sealed class UploadedEventArgs(int days) : EventArgs
{
    public int Days { get; } = days;
}
