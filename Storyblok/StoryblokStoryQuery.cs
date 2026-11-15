using Storyblok.Client;

namespace Storyblok;

/// <summary>Describes the story to load: <c>client.Story().WithSlug("/home").TryLoad&lt;Page&gt;()</c>.</summary>
public sealed class StoryblokStoryQuery(StoryblokStoryClient client)
{
    private string _slug = string.Empty;

    public StoryblokStoryQuery WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    /// <summary>
    /// Loads the story. Instead of throwing, a story that doesn't exist is <see cref="StoryNotFound"/>, a Storyblok outage
    /// is <see cref="StoryblokUnavailable"/> and content that doesn't fit <typeparamref name="T"/> is <see cref="StoryUnreadable"/>,
    /// so a page can still render without its content.
    /// </summary>
    public Task<StoryLoadResult<T>> TryLoad<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        return client.TryLoadStory<T>(_slug, cancellationToken);
    }

    /// <summary>
    /// Asks Storyblok again for a story the page came with (see <see cref="PrerenderedStories"/>), past every cache. Null when
    /// the page came without it or the story hasn't changed since; otherwise the outcome, like <see cref="TryLoad{T}"/>.
    /// </summary>
    public Task<StoryLoadResult<T>?> TryRefresh<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        return client.TryRefreshStory<T>(_slug, cancellationToken);
    }
}
