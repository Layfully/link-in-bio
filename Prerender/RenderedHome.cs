namespace Prerender;

/// <summary>The home page as the app renders it into <c>#app</c>, and what it shows about the person.</summary>
/// <param name="Html">What the app renders into <c>#app</c> for <c>/</c>.</param>
/// <param name="Profile">The person as the page shows them, for its metadata.</param>
/// <param name="LinksWithoutAddress">
/// The labels of listed links that show as "not available": their address is empty in Storyblok, or the site refused it
/// (StoryblokUrlConverter), such as <c>www.example.com</c> typed without <c>https://</c>. The deploy warns about each.
/// </param>
public sealed record RenderedHome(string Html, SiteProfile Profile, IReadOnlyList<string> LinksWithoutAddress);

/// <summary>
/// The person behind the site as Storyblok describes them, for the page's metadata (link previews, search engines).
/// </summary>
/// <param name="Image">The profile photo, cut to 600 x 600; null when the story has none that Storyblok can resize.</param>
/// <param name="SameAs">The profiles the page links to, as absolute web addresses.</param>
public sealed record SiteProfile(string? Image, IReadOnlyList<string> SameAs)
{
    /// <summary>Nothing known: the page's metadata stays as written.</summary>
    public static SiteProfile None { get; } = new(null, []);
}
