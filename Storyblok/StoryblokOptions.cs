namespace Storyblok;

public sealed class StoryblokOptions
{
    /// <summary>
    /// The public token, which reads published content. It ships with the site, so anyone can read it; that is what it's for.
    /// </summary>
    public string? ApiKeyPublic { get; set; }

    /// <summary>
    /// The preview token, which also reads drafts. Only the build for the Visual Editor has it; production never does,
    /// because everything in a WebAssembly app's configuration can be downloaded by any visitor.
    /// </summary>
    public string? ApiKeyPreview { get; set; }

    /// <summary>
    /// True while the app runs inside the Storyblok Visual Editor (and has the preview token).
    /// Stories are then loaded as drafts with the preview token and are never cached.
    /// </summary>
    public bool IsInEditor { get; set; }

    public string BaseUrl { get; set; } = "https://api.storyblok.com/v2/cdn";

    /// <summary>
    /// The duration (in seconds) that all loaded stories will be cached locally.
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 60 * 15;
}
