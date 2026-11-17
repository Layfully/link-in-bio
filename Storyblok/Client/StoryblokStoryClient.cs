using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyblok.Converters;
using Storyblok.Extensions;
using System.Net;
using System.Text.Json;

namespace Storyblok.Client;

/// <summary>Loads stories from Storyblok's Content Delivery API v2.</summary>
public sealed class StoryblokStoryClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly StoryCache _cache;
    private readonly PrerenderedStories _prerendered;
    private readonly ILogger<StoryblokStoryClient> _logger;
    private readonly StoryblokOptions _settings;

    // Built once: System.Text.Json caches what it learns about each type inside its options, so new options for every
    // request would repeat that reflection each time.
    private readonly JsonSerializerOptions _jsonOptions;

    // The cache version from the last Storyblok response. Sending it back lets the CDN answer requests for published
    // content directly instead of redirecting to the latest version first (https://www.storyblok.com/docs/concepts/caching).
    // It is sent only as long as stories are cached: the CDN answers a cache version it has seen before with the content
    // of that version, so a cached story loaded again with it would come back unchanged after every publish.
    private long? _cacheVersion;
    private DateTimeOffset _cacheVersionExpires;
    private readonly TimeProvider _time;

    // The handler sends the requests: in the app the retry handler around the browser's (see AddStoryblok), in tests a
    // fake Storyblok. It is shared, so disposing this client leaves it alone.
    public StoryblokStoryClient(
        IOptions<StoryblokOptions> settings,
        StoryblokComponentTypes componentTypes,
        [FromKeyedServices(ServiceCollectionExtensions.HttpHandlerKey)] HttpMessageHandler handler,
        PrerenderedStories prerendered,
        TimeProvider time,
        ILogger<StoryblokStoryClient> logger)
    {
        // Reading Value validates the options (see AddStoryblok), so a missing token fails here with a clear message.
        _settings = settings.Value;
        // The handler limits each attempt and the whole request, but only until the answer's headers arrive; the body is
        // read afterwards. This limit covers that too, so a body that stalls can't keep the page loading for the
        // default 100 seconds.
        _client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = RetryPolicy.Default.TotalTimeout + RetryPolicy.Default.AttemptTimeout,
        };
        _cache = new StoryCache(time);
        _time = time;
        _prerendered = prerendered;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            Converters =
            {
                new StoryblokComponentConverter(componentTypes),
                new StoryblokStringConverter(),
                new StoryblokAssetConverter(),
            },
        };
    }

    // Drafts are loaded only inside the Visual Editor, with the preview token; everywhere else published content with the public one.
    private bool LoadDrafts => _settings.IsInEditor;

    private string ApiKey => (LoadDrafts ? _settings.ApiKeyPreview : _settings.ApiKeyPublic) ?? string.Empty;

    public StoryblokStoryQuery Story() => new(this);

    public void Dispose() => _client.Dispose();

    /// <summary>A story as it is being edited in the Visual Editor, unsaved; pages showing it re-render from it.</summary>
    public event Action<StoryblokEdit>? StoryEdited;

    /// <summary>
    /// Reads a story the Visual Editor's bridge sent while it is edited and raises <see cref="StoryEdited"/> with it.
    /// Ignored outside the editor. Not cached: drafts never are, and the next save reloads the page anyway. A story that
    /// can't be read is only logged, so a page keeps what it shows rather than turning into an error mid-edit.
    /// </summary>
    public void ReadEditedStory(string storyJson)
    {
        if (!LoadDrafts)
        {
            return;
        }

        StoryblokStory? story;
        try
        {
            story = JsonSerializer.Deserialize<StoryblokStory>(storyJson, _jsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "A story edited in the Visual Editor can't be read; the preview keeps what it shows.");
            return;
        }

        if (story is null)
        {
            _logger.LogWarning("The Visual Editor sent no story; the preview keeps what it shows.");
            return;
        }

        StoryEdited?.Invoke(new StoryblokEdit(story));
    }

    internal Task<StoryLoadResult<T>> TryLoadStory<T>(string slug, CancellationToken cancellationToken) where T : StoryblokComponent
    {
        if (ToStoryPath(slug) is not { } path)
        {
            _logger.LogTrace("Slug {Slug} can't name a story.", slug);
            return Task.FromResult<StoryLoadResult<T>>(new StoryNotFound());
        }

        return ToResult<T>(slug, () => LoadStory(path, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Asks Storyblok again for a story the page came with (see <see cref="PrerenderedStories"/>), past every cache. Null
    /// when the page came without it, or when Storyblok's story is the same as the one it came with.
    /// </summary>
    internal async Task<StoryLoadResult<T>?> TryRefreshStory<T>(string slug, CancellationToken cancellationToken) where T : StoryblokComponent
    {
        if (LoadDrafts || ToStoryPath(slug) is not { } path || !_prerendered.TryGetResponse(path, out string prerendered))
        {
            return null;
        }

        bool unchanged = false;
        StoryLoadResult<T> result = await ToResult<T>(slug, async () =>
        {
            (StoryblokStory? story, string? response) = await LoadStoryFromStoryblok(path, cancellationToken);
            unchanged = IsSameStory(prerendered, response);

            // A newer story takes the prerendered one's place in the cache, for the rest of the visit.
            if (story is not null && !unchanged)
            {
                Remember(path, story);
            }

            return story;
        }, cancellationToken);

        return unchanged ? null : (StoryLoadResult<T>?)result;
    }

    // Compared as JSON values, not as text: the embedded copy has <, > and & written as \u escapes.
    private static bool IsSameStory(string response, string? otherResponse)
    {
        if (otherResponse is null)
        {
            return false;
        }

        try
        {
            using JsonDocument first = JsonDocument.Parse(response);
            using JsonDocument second = JsonDocument.Parse(otherResponse);
            return first.RootElement.TryGetProperty("story", out JsonElement firstStory)
                && second.RootElement.TryGetProperty("story", out JsonElement secondStory)
                && JsonElement.DeepEquals(firstStory, secondStory);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<StoryLoadResult<T>> ToResult<T>(string slug, Func<Task<StoryblokStory?>> load, CancellationToken cancellationToken)
        where T : StoryblokComponent
    {
        StoryblokStory? story;

        try
        {
            story = await load();
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
        // The first load of a story the page came with needs no request. Drafts never come with the page.
        if (!LoadDrafts && FromPrerendered(path) is { } prerendered)
        {
            return prerendered;
        }

        // Drafts in the Visual Editor must always be fresh. A cache duration of 0 turns caching off.
        if (LoadDrafts || _settings.CacheDurationSeconds <= 0)
        {
            return (await LoadStoryFromStoryblok(path, cancellationToken)).Story;
        }

        // A story Storyblok said doesn't exist is cached too (as null), so a mistyped address isn't asked about again.
        if (_cache.TryGet(path, out StoryblokStory? cached))
        {
            _logger.LogTrace("Using the cached answer for {Path}.", path);
            return cached;
        }

        StoryblokStory? story = (await LoadStoryFromStoryblok(path, cancellationToken)).Story;
        Remember(path, story);
        return story;
    }

    // Caches an answer (a story, or null for one that doesn't exist) the way a load from Storyblok does, unless caching
    // is off.
    private void Remember(string path, StoryblokStory? story)
    {
        if (!LoadDrafts && _settings.CacheDurationSeconds > 0)
        {
            _cache.Set(path, story, TimeSpan.FromSeconds(_settings.CacheDurationSeconds));
        }
    }

    // A prerendered response that can't be read is skipped: the story then loads from Storyblok as if the page had come
    // without it.
    private StoryblokStory? FromPrerendered(string path)
    {
        if (!_prerendered.TryTake(path, out string response))
        {
            return null;
        }

        try
        {
            if (JsonSerializer.Deserialize<StoryblokStoryContainer>(response, _jsonOptions)?.Story is { } story)
            {
                _logger.LogTrace("Using the prerendered story for {Path}.", path);
                Remember(path, story);
                return story;
            }
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "The prerendered story for {Path} can't be read; loading it from Storyblok instead.", path);
        }

        return null;
    }

    // Links are left unresolved (Storyblok's default): a link field carries the target's slug, which is all the site needs.
    // Also returns the response as Storyblok sent it, for recording (PrerenderedStories) and for comparing on a refresh.
    private async Task<(StoryblokStory? Story, string? Response)> LoadStoryFromStoryblok(string path, CancellationToken cancellationToken)
    {
        List<string> query = [$"version={(LoadDrafts ? "draft" : "published")}"];

        // Drafts must always be fresh. Published content comes from the CDN for the cache version last seen, while that
        // is recent; after that, a request without one gets the latest.
        bool sentCacheVersion = false;
        if (!LoadDrafts && _cacheVersion is { } cacheVersion && _time.GetUtcNow() < _cacheVersionExpires)
        {
            query.Add($"cv={cacheVersion}");
            sentCacheVersion = true;
        }

        string request = $"stories/{path}?{string.Join('&', query)}";

        // Logged without the token, which is added only to the address actually requested.
        _logger.LogTrace("Loading {Request} from Storyblok.", request);

        using HttpResponseMessage response = await _client.GetAsync(
            $"{_settings.BaseUrl.TrimEnd('/')}/{request}&token={Uri.EscapeDataString(ApiKey)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        StoryblokStoryContainer? container = JsonSerializer.Deserialize<StoryblokStoryContainer>(body, _jsonOptions);

        if (container?.Story is not { } story)
        {
            throw new JsonException($"Storyblok's response for {request} contains no story.");
        }

        // Only a cache version that is new to the client starts its time. An answer to a request that sent one carries the
        // same one back; counting from that answer would keep an old version alive as long as the visitor keeps loading
        // stories, and a story whose cache entry ran out would then come back unchanged.
        if (container.CacheVersion > 0 && (!sentCacheVersion || container.CacheVersion != _cacheVersion))
        {
            _cacheVersion = container.CacheVersion;
            _cacheVersionExpires = _time.GetUtcNow() + TimeSpan.FromSeconds(Math.Max(_settings.CacheDurationSeconds, 0));
        }

        _prerendered.Record(path, body);
        return (story, body);
    }
}
