namespace Client;

/// <summary>Facts about the public site that pages put into their head.</summary>
public static class Site
{
    /// <summary>
    /// The public address, without a trailing slash. Canonical links point here from every environment, so previews and
    /// the editor environment never compete with the real site in search results.
    /// </summary>
    public const string Url = "https://adriangaborek.dev";
}
