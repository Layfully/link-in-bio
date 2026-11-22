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
    /// Loads the story and returns its content or a typed failure result.
    /// </summary>
    public Task<StoryLoadResult<T>> TryLoad<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
        => client.TryLoadStory<T>(_slug, cancellationToken);

    /// <summary>
    /// Refreshes a prerendered story. Returns <c>null</c> when there's nothing to refresh or the story is unchanged.
    /// </summary>
    public Task<StoryLoadResult<T>?> TryRefresh<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
        => client.TryRefreshStory<T>(_slug, cancellationToken);
}
