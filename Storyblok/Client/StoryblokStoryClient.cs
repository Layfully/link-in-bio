using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyblok.Converters;
using Storyblok.Extensions;
using System.Net;
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

    internal async Task<StoryLoadResult<T>> TryLoadStory<T>(string slug, CancellationToken cancellationToken) where T : StoryblokComponent
    {
        if (ToStoryPath(slug) is not { } path)
        {
            _logger.LogTrace("Slug {Slug} can't name a story.", slug);
            return new StoryNotFound();
        }

        StoryblokStory? story;

        try
        {
            story = await LoadStory(path, cancellationToken);
        }
        catch (JsonException exception)
        {
            // Storyblok answered, but with something the site can't read. Asking again would get the same answer.
            _logger.LogError(exception, "Story {Slug} can't be read: Storyblok's response doesn't fit the site's component classes.", slug);
            return new StoryUnreadable(exception);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Network errors, timeouts and error answers (5xx, also 401 for a wrong token) mean the content can't be loaded
            // right now. A cancellation the caller asked for (the page went away) is not an outage, so it propagates instead.
            _logger.LogError(exception, "Storyblok is unavailable, story {Slug} could not be loaded.", slug);
            return new StoryblokUnavailable(exception);
        }

        if (story is null)
        {
            return new StoryNotFound();
        }

        // A story of another kind, such as a page published under a certificate's address, or one without a content block.
        if (story.Content is not T content)
        {
            InvalidOperationException exception = new(story.Content is null
                ? $"The story has no content block, not {typeof(T)}."
                : $"The story's content is a \"{story.Content.Component}\" component ({story.Content.GetType()}), not {typeof(T)}.");
            _logger.LogError(exception, "Story {Slug} can't be read: its content block is of another kind.", slug);
            return new StoryUnreadable(exception);
        }

        return new StoryblokStory<T>(story.Name, content);
    }

    /// <summary>
    /// The slug as it goes into the request path, each segment escaped: callers pass slugs as they are, also ones built
    /// from the page address. Null for a slug that can't name a story: an empty segment, or "." and "..", which the
    /// address would resolve to another path than the one asked for.
    /// </summary>
    internal static string? ToStoryPath(string slug)
    {
        string[] segments = slug.Trim('/').Split('/');

        return segments.Any(segment => segment is "" or "." or "..")
            ? null
            : string.Join('/', segments.Select(Uri.EscapeDataString));
    }

    // Takes the escaped path, which is also the cache key: "/home" and "home" are one story and one cache entry.
    private async Task<StoryblokStory?> LoadStory(string path, CancellationToken cancellationToken)
    {
        // Drafts in the Visual Editor must always be fresh. A cache duration of 0 turns caching off (MemoryCache rejects
        // an entry that expires at once).
        if (LoadDrafts || _settings.CacheDurationSeconds <= 0)
        {
            return await LoadStoryFromStoryblok(path, cancellationToken);
        }

        string cacheKey = path;
        if (_cache.TryGetValue(cacheKey, out StoryblokStory? cachedStory) && cachedStory is not null)
        {
            _logger.LogTrace("Using cached story for {Path}.", path);
            return cachedStory;
        }

        string notFoundCacheKey = "404_" + cacheKey;
        if (_cache.TryGetValue(notFoundCacheKey, out _))
        {
            _logger.LogTrace("Using cached 404 for {Path}.", path);
            return null;
        }

        TimeSpan cacheDuration = TimeSpan.FromSeconds(_settings.CacheDurationSeconds);
        StoryblokStory? story = await LoadStoryFromStoryblok(path, cancellationToken);

        if (story is not null)
        {
            _cache.Set(cacheKey, story, cacheDuration);
            return story;
        }

        _logger.LogTrace("Story not found for {Path}.", path);
        _cache.Set(notFoundCacheKey, true, cacheDuration);
        return null;
    }

    // Links are left unresolved (Storyblok's default): a link field carries the target's slug, which is all the site needs.
    private async Task<StoryblokStory?> LoadStoryFromStoryblok(string path, CancellationToken cancellationToken)
    {
        List<string> query = [$"version={(LoadDrafts ? "draft" : "published")}"];

        // drafts must always be fresh; published content is served from the CDN for the cache version we last saw
        if (!LoadDrafts && _cacheVersion is { } cacheVersion)
        {
            query.Add($"cv={cacheVersion}");
        }

        string request = $"stories/{path}?{string.Join('&', query)}";

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
