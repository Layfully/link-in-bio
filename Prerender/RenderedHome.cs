namespace Prerender;

/// <summary>Home page HTML for <c>#app</c>, profile metadata, and labels of links with missing or rejected URLs.</summary>
public sealed record RenderedHome(string Html, SiteProfile Profile, IReadOnlyList<string> LinksWithoutAddress);

/// <summary>Profile metadata for the home page.</summary>
/// <param name="Image">URL of the 600 × 600 preview image, if available.</param>
/// <param name="SameAs">Absolute profile URLs for the person's structured data.</param>
public sealed record SiteProfile(string? Image, IReadOnlyList<string> SameAs)
{
    public static SiteProfile None { get; } = new(null, []);
}
