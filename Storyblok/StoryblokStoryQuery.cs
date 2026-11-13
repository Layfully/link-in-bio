using Storyblok.Client;
using System.Globalization;

namespace Storyblok;

public class StoryblokStoryQuery
{
    private readonly StoryblokStoryClient _client;

    private CultureInfo? _culture;
    private string _slug = string.Empty;
    private ResolveLinksType _resolveLinks = ResolveLinksType.Url;
    private bool _resolveAssets;
    private string _resolveRelations = string.Empty;

    public StoryblokStoryQuery(StoryblokStoryClient client)
    {
        _client = client;
    }

    public StoryblokStoryQuery WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    public StoryblokStoryQuery WithCulture(CultureInfo culture)
    {
        _culture = culture;
        return this;
    }

    public StoryblokStoryQuery ResolveLinks(ResolveLinksType type)
    {
        _resolveLinks = type;
        return this;
    }

    public StoryblokStoryQuery ResolveAssets(bool resolveAssets = true)
    {
        _resolveAssets = resolveAssets;
        return this;
    }

    public StoryblokStoryQuery ResolveRelations(string relations)
    {
        _resolveRelations = relations;
        return this;
    }

    public async Task<StoryblokStory<T>?> Load<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        return await _client.LoadStory<T>(_culture, _slug, _resolveLinks, _resolveAssets, _resolveRelations, cancellationToken);
    }

    /// <summary>
    /// Loads the story like <see cref="Load{T}"/>, but reports a Storyblok outage as <see cref="StoryblokUnavailable"/> instead of throwing,
    /// so a page can still render without its content.
    /// </summary>
    public Task<StoryLoadResult<T>> TryLoad<T>(CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        return _client.TryLoadStory<T>(_culture, _slug, _resolveLinks, _resolveAssets, _resolveRelations, cancellationToken);
    }

    public async Task<StoryblokStory?> Load(CancellationToken cancellationToken = default)
    {
        return await _client.LoadStory(_culture, _slug, _resolveLinks, _resolveAssets, _resolveRelations, cancellationToken);
    }
}

public enum ResolveLinksType
{
    Url,
    Story,
    Link,
    None
}
