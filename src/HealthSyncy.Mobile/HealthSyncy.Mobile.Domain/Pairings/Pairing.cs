namespace HealthSyncy.Mobile.Domain.Pairings;

public sealed record Pairing(string Origin, string Token)
{
    public static Pairing Create(string? address, string? token)
    {
        var origin = OriginOf(address)
            ?? throw new SyncException("Enter the sync server address, such as https://sync.example.org.");
        return string.IsNullOrWhiteSpace(token)
            ? throw new SyncException("Paste the device upload token.")
            : new(origin, token.Trim());
    }

    private static string? OriginOf(string? address)
    {
        var text = address?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out var url)
            && url is { Scheme: "https", UserInfo.Length: 0, PathAndQuery: "/", Fragment.Length: 0 }
                ? url.GetLeftPart(UriPartial.Authority)
                : null;
    }
}
