using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyblok.Converters;
using Storyblok.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Storyblok.Client;

/// <summary>Loads stories from Storyblok's Content Delivery API v2.</summary>
public sealed class StoryblokStoryClient
{
    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StoryblokStoryClient> _logger;
    private readonly StoryblokOptions _settings;

    // Built once: System.Text.Json caches what it learns about each type inside its options, so new options for every
    // request would repeat that reflection each time.
    private readonly JsonSerializerOptions _jsonOptions;

    // The cache version from the last Storyblok response. Sending it back lets the CDN answer requests for published
    // content directly instead of redirecting to the latest version first (https://www.storyblok.com/docs/concepts/caching).
    private long? _cacheVersion;

    public StoryblokStoryClient(
        IOptions<StoryblokOptions> settings,
        StoryblokComponentTypes componentTypes,
        IHttpClientFactory clientFactory,
        IMemoryCache cache,
        ILogger<StoryblokStoryClient> logger)
    {
        // Reading Value validates the options (see AddStoryblok), so a missing token fails here with a clear message.
        _settings = settings.Value;
        _client = clientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
        _cache = cache;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new StoryblokComponentConverter(componentTypes),
                new StoryblokDateConverter(),
                new StoryblokNullableDateConverter(),
                new StoryblokIntConverter(),
                new StoryblokNullableIntConverter(),
                new StoryblokStringConverter(),
                new StoryblokAssetConverter(),
            },
        };
    }

    // Drafts are loaded only inside the Visual Editor, with the preview token; everywhere else published content with the public one.
    private bool LoadDrafts => _settings.IsInEditor;

    private string ApiKey => (LoadDrafts ? _settings.ApiKeyPreview : _settings.ApiKeyPublic) ?? string.Empty;

    public StoryblokStoryQuery Story() => new(this);

    internal async Task<StoryLoadResult<T>> TryLoadStory<T>(string slug, ResolveLinksType resolveLinks, CancellationToken cancellationToken) where T : StoryblokComponent
    {
        try
        {
            StoryblokStory? story = await LoadStory(slug, resolveLinks, cancellationToken);

            if (story is null)
            {
                return new StoryNotFound();
            }

            return new StoryblokStory<T>(story);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Network errors, timeouts, 5xx and 401 answers and unreadable responses all mean the content can't be shown right now.
            // A cancellation the caller asked for (the page went away) is not an outage, so it propagates instead.
            _logger.LogError(exception, "Storyblok is unavailable, story {Slug} could not be loaded.", slug);
            return new StoryblokUnavailable(exception);
        }
    }

    private async Task<StoryblokStory?> LoadStory(string slug, ResolveLinksType resolveLinks, CancellationToken cancellationToken)
    {
        // Drafts in the Visual Editor must always be fresh.
        if (LoadDrafts)
        {
            return await LoadStoryFromStoryblok(slug, resolveLinks, cancellationToken);
        }

        string cacheKey = $"{slug}_{resolveLinks}";
        if (_cache.TryGetValue(cacheKey, out StoryblokStory? cachedStory) && cachedStory is not null)
        {
            _logger.LogTrace("Using cached story for slug {Slug}.", slug);
            return cachedStory;
        }

        string notFoundCacheKey = "404_" + cacheKey;
        if (_cache.TryGetValue(notFoundCacheKey, out _))
        {
            _logger.LogTrace("Using cached 404 for slug {Slug}.", slug);
            return null;
        }

        TimeSpan cacheDuration = TimeSpan.FromSeconds(_settings.CacheDurationSeconds);
        StoryblokStory? story = await LoadStoryFromStoryblok(slug, resolveLinks, cancellationToken);

        if (story is not null)
        {
            _cache.Set(cacheKey, story, cacheDuration);
            return story;
        }

        _logger.LogTrace("Story not found for slug {Slug}.", slug);
        _cache.Set(notFoundCacheKey, true, cacheDuration);
        return null;
    }

    private async Task<StoryblokStory?> LoadStoryFromStoryblok(string slug, ResolveLinksType resolveLinks, CancellationToken cancellationToken)
    {
        List<string> query = [$"version={(LoadDrafts ? "draft" : "published")}"];

        // drafts must always be fresh; published content is served from the CDN for the cache version we last saw
        if (!LoadDrafts && _cacheVersion is { } cacheVersion)
        {
            query.Add($"cv={cacheVersion}");
        }

        if (resolveLinks != ResolveLinksType.None)
        {
            query.Add($"resolve_links={resolveLinks.ToString().ToLowerInvariant()}");
        }

        string request = $"stories/{slug.TrimStart('/')}?{string.Join('&', query)}";

        // Logged without the token, which is added only to the address actually requested.
        _logger.LogTrace("Loading {Request} from Storyblok.", request);

        using HttpResponseMessage response = await _client.GetAsync(
            $"{_settings.BaseUrl.TrimEnd('/')}/{request}&token={Uri.EscapeDataString(ApiKey)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        StoryblokStoryContainer? container = await response.Content.ReadFromJsonAsync<StoryblokStoryContainer>(_jsonOptions, cancellationToken);

        if (container?.Story is not { } story)
        {
            throw new JsonException($"Storyblok's response for {request} contains no story.");
        }

        if (container.CacheVersion > 0)
        {
            _cacheVersion = container.CacheVersion;
        }

        return story;
    }
}
