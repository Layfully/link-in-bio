namespace Storyblok;

public class StoryblokOptions
{
    public string? ApiKeyPreview { get; set; }
    public string? ApiKeyPublic { get; set; }
    public bool IncludeDraftStories { get; set; }

    /// <summary>
    /// True while the app runs inside the Storyblok Visual Editor, which opens the preview with a <c>_storyblok</c> query parameter.
    /// In the editor, stories are loaded as drafts with the preview token and are never cached.
    /// </summary>
    public bool IsInEditor { get; set; }

    public string BaseUrl { get; set; } = "https://api.storyblok.com/v2/cdn";

    /// <summary>
    /// The duration (in seconds) that all loaded stories will be cached locally.
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 60 * 15;

    /// <summary>
    /// The supported cultures, as specified in Storyblok.
    /// The first culture is also the default culture.
    /// </summary>
    public string[] SupportedCultures { get; set; } = [];
}
