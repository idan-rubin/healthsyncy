namespace HealthSyncy.Mobile.Domain;

public sealed class SyncException : Exception
{
    public SyncException()
    {
    }

    public SyncException(string message) : base(message)
    {
    }

    public SyncException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
