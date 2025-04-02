using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Storyblok.Client;

public class StoryblokStoryClient : StoryblokBaseClient
{
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

    internal async Task<StoryblokStory<T>?> LoadStory<T>(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations) where T : StoryblokComponent
    {
        StoryblokStory? story = await LoadStory(culture, slug, resolveLinks, resolveAssets, resolveRelations);

        if (story is null)
        {
            return null;
        }

        return new StoryblokStory<T>(story);
    }

    internal async Task<StoryblokStory?> LoadStory(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations)
    {
        if (IsInEditor)
        {
            return await LoadStoryFromStoryblok(culture, slug, resolveLinks, resolveAssets, resolveRelations);
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
        var story = await LoadStoryFromStoryblok(culture, slug, resolveLinks, resolveAssets, resolveRelations);
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

    private async Task<StoryblokStory?> LoadStoryFromStoryblok(CultureInfo? culture, string slug, ResolveLinksType resolveLinks, bool resolveAssets, string resolveRelations)
    {
        CultureInfo defaultCulture = new(Settings.SupportedCultures.First());
        CultureInfo currentCulture = defaultCulture;

        if (culture is not null)
        {
            // only use the culture if it's actually supported
            // use only the short culture "en", if we get a full culture "en-US" but only support the short one
            string[] matchingCultures = Settings.SupportedCultures
                .Where(x => x.Equals(culture.ToString(), StringComparison.OrdinalIgnoreCase) || x.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Length)
                .ToArray();

            if (matchingCultures.Length != 0)
            {
                currentCulture = new CultureInfo(matchingCultures.First());
            }
        }

        string url = $"{Settings.BaseUrl}/stories/{slug}?token={ApiKey}";

        if (!currentCulture.Equals(defaultCulture))
        {
            url = $"{Settings.BaseUrl}/stories/{currentCulture.ToString().ToLower()}/{slug}?token={ApiKey}";
        }

        url += $"&cb={DateTime.UtcNow:yyyyMMddHHmmss}";

        if (resolveLinks != ResolveLinksType.None)
        {
            url += $"&resolve_links={resolveLinks.ToString().ToLower()}";
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

        if (Settings.IncludeDraftStories || IsInEditor)
        {
            url += "&version=draft";
        }

        Logger.LogTrace($"Loading {url} ...");

        HttpResponseMessage response = await Client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadAsStringAsync();

        StoryblokStory? story = JsonSerializer.Deserialize<StoryblokStoryContainer>(responseString, JsonOptions)?.Story;

        if (story is null)
        {
            throw new Exception($"Unable to deserialize {responseString}.");
        }

        story.LoadedAt = DateTime.UtcNow;
        return story;
    }
}
