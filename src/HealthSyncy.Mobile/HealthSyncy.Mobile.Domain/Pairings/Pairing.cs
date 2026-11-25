namespace HealthSyncy.Mobile.Domain.Pairings;

public sealed record Pairing(string Origin, string Token)
{
    public static Pairing Create(string? origin, string? token) =>
        !string.IsNullOrWhiteSpace(token) && Uri.TryCreate(origin?.Trim(), UriKind.Absolute, out var url)
        && url is { Scheme: "https", UserInfo.Length: 0, PathAndQuery: "/", Fragment.Length: 0 }
            ? new(url.GetLeftPart(UriPartial.Authority), token)
            : throw new SyncException("Enter an HTTPS origin and device token.");
}
