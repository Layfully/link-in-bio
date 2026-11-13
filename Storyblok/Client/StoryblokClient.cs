using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Storyblok.Client;

public class StoryblokStoryClient : StoryblokBaseClient
{
    // The cache version from the last Storyblok response. Sending it back lets the CDN answer requests for published
    // content directly instead of redirecting to the latest version first (https://www.storyblok.com/docs/concepts/caching).
    private long? _cacheVersion;

    public StoryblokStoryClient(
        IOptions<StoryblokOptions> settings,
        IHttpClientFactory clientFactory,
        IMemoryCache memoryCache,
        ILogger<StoryblokBaseClient> logger) : base(settings, clientFactory, memoryCache, logger)
    {
    }

    public StoryblokStoryQuery Story()
    {
        return new(this);
    }

    internal async Task<StoryblokStory<T>?> LoadStory<T>(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations, CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        StoryblokStory? story = await LoadStory(culture, slug, resolveLinks, resolveAssets, resolveRelations, cancellationToken);

        if (story is null)
        {
            return null;
        }

        return new StoryblokStory<T>(story);
    }

    internal async Task<StoryLoadResult<T>> TryLoadStory<T>(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations, CancellationToken cancellationToken = default) where T : StoryblokComponent
    {
        try
        {
            StoryblokStory<T>? story = await LoadStory<T>(culture, slug, resolveLinks, resolveAssets, resolveRelations, cancellationToken);

            if (story is null)
            {
                return new StoryNotFound();
            }

            return story;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Network errors, timeouts, 5xx and 401 answers and unreadable responses all mean the content can't be shown right now.
            // A cancellation the caller asked for (the page went away) is not an outage, so it propagates instead.
            Logger.LogError(exception, "Storyblok is unavailable, story {slug} (culture {culture}) could not be loaded.", slug, culture);
            return new StoryblokUnavailable(exception);
        }
    }

    internal async Task<StoryblokStory?> LoadStory(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations, CancellationToken cancellationToken = default)
    {
        if (IsInEditor)
        {
            return await LoadStoryFromStoryblok(culture, slug, resolveLinks, resolveAssets, resolveRelations, cancellationToken);
        }

        string cacheKey = $"{culture}_{slug}_{resolveLinks}_{resolveAssets}_{resolveRelations}";
        if (MemoryCache.TryGetValue(cacheKey, out StoryblokStory? cachedStory) && cachedStory is not null)
        {
            Logger.LogTrace("Using cached story for slug {slug} (culture {culture}).", slug, culture);
            return cachedStory;
        }

        var cacheKeyUnavailable = "404_" + cacheKey;
        if (MemoryCache.TryGetValue(cacheKeyUnavailable, out _))
        {
            Logger.LogTrace("Using cached 404 for slug {slug} (culture {culture}).", slug, culture);
            return null;
        }

        Logger.LogTrace("Trying to load story for slug {slug} (culture {culture}).", slug, culture);
        var story = await LoadStoryFromStoryblok(culture, slug, resolveLinks, resolveAssets, resolveRelations, cancellationToken);
        if (story is not null)
        {
            Logger.LogTrace("Story loaded for slug {slug} (culture {culture}).", slug, culture);
            MemoryCache.Set(cacheKey, story, TimeSpan.FromSeconds(Settings.CacheDurationSeconds));
            return story;
        }

        Logger.LogTrace("Story not found for slug {slug} (culture {culture}).", slug, culture);
        MemoryCache.Set(cacheKeyUnavailable, true, TimeSpan.FromSeconds(Settings.CacheDurationSeconds));
        return null;
    }

    private async Task<StoryblokStory?> LoadStoryFromStoryblok(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations, CancellationToken cancellationToken)
    {
        CultureInfo defaultCulture = new(Settings.SupportedCultures.First());
        CultureInfo currentCulture = defaultCulture;

        if (culture is not null)
        {
            // only use the culture if it's actually supported
            // use only the short culture "en", if we get a full culture "en-US" but only support the short one
            // The language is taken from the name ("de-AT" -> "de"): with invariant globalization, as in the browser build,
            // TwoLetterISOLanguageName has no culture data to derive it from.
            string language = culture.Name.Split('-')[0];
            string[] matchingCultures = Settings.SupportedCultures
                .Where(x => x.Equals(culture.Name, StringComparison.OrdinalIgnoreCase) || x.Equals(language, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Length)
                .ToArray();

            if (matchingCultures.Length != 0)
            {
                currentCulture = new CultureInfo(matchingCultures.First());
            }
        }

        bool loadDraft = Settings.IncludeDraftStories || IsInEditor;

        string url = $"{Settings.BaseUrl}/stories/{slug}?token={ApiKey}&version={(loadDraft ? "draft" : "published")}";

        // API v2 selects the language with a query parameter instead of a slug prefix
        if (!currentCulture.Equals(defaultCulture))
        {
            url += $"&language={currentCulture.Name.ToLowerInvariant()}";
        }

        // drafts must always be fresh; published content is served from the CDN for the cache version we last saw
        if (!loadDraft && _cacheVersion is { } cacheVersion)
        {
            url += $"&cv={cacheVersion}";
        }

        if (resolveLinks != ResolveLinksType.None)
        {
            url += $"&resolve_links={resolveLinks.ToString().ToLowerInvariant()}";
        }

        // should only work in Premium Plans, (as per https://www.storyblok.com/docs/api/content-delivery/v2)
        if (resolveAssets)
        {
            url += "&resolve_assets=1";
        }

        if (!string.IsNullOrWhiteSpace(resolveRelations))
        {
            url += $"&resolve_relations={resolveRelations}";
        }

        Logger.LogTrace($"Loading {url} ...");

        HttpResponseMessage response = await Client.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        StoryblokStoryContainer? container = JsonSerializer.Deserialize<StoryblokStoryContainer>(responseString, JsonOptions);

        if (container?.Story is not { } story)
        {
            throw new Exception($"Unable to deserialize {responseString}.");
        }

        if (container.CacheVersion > 0)
        {
            _cacheVersion = container.CacheVersion;
        }

        story.LoadedAt = DateTime.UtcNow;
        return story;
    }
}
