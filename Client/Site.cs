namespace Client;

public sealed class Site(string url)
{
    /// <summary>Canonical site URL without a trailing slash.</summary>
    public string Url { get; } = ParseOrigin(url) ?? throw new ArgumentException("Expected an HTTP or HTTPS origin.", nameof(url));
    public const string Name = "Links";

    public static Site FromConfiguration(IConfiguration configuration) =>
        new(configuration["Site:Url"] ?? throw new InvalidOperationException("Site:Url is missing."));

    public static string? ParseOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "http" or "https"
        && uri.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6
        && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : null;
}
