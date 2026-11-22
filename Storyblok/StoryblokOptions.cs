namespace Storyblok;

public sealed class StoryblokOptions
{
    /// <summary>The public token for published content, included in the site build.</summary>
    public string? ApiKeyPublic { get; set; }

    /// <summary>The token for reading drafts, included only in the Visual Editor build.</summary>
    public string? ApiKeyPreview { get; set; }

    /// <summary>Loads uncached drafts using the preview token when the Visual Editor is active.</summary>
    public bool IsInEditor { get; set; }

    public string BaseUrl { get; set; } = "https://api.storyblok.com/v2/cdn";

    /// <summary>Local cache lifetime in seconds. Zero disables caching.</summary>
    public int CacheDurationSeconds { get; set; } = 60 * 15;
}
