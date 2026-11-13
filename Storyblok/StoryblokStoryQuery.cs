using Storyblok.Client;

namespace Storyblok;

/// <summary>Describes the story to load: <c>client.Story().WithSlug("/home").TryLoad&lt;Page&gt;()</c>.</summary>
public sealed class StoryblokStoryQuery(StoryblokStoryClient client)
{
    private string _slug = string.Empty;
    private ResolveLinksType _resolveLinks = ResolveLinksType.Url;

    public StoryblokStoryQuery WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    public StoryblokStoryQuery ResolveLinks(ResolveLinksType type)
    {
        _resolveLinks = type;
        return this;
    }

    /// <summary>
    /// Loads the story. A story that doesn't exist is <see cref="StoryNotFound"/>; a Storyblok outage is
    /// <see cref="StoryblokUnavailable"/> instead of an exception, so a page can still render without its content.
    /// </summary>
    public Task<StoryLoadResult<T>> TryLoad<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        return client.TryLoadStory<T>(_slug, _resolveLinks, cancellationToken);
    }
}

public enum ResolveLinksType
{
    Url,
    Story,
    Link,
    None
}
