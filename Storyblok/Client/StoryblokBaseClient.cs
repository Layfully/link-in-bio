using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyblok.Converters;
using System.Globalization;
using System.Text.Json;

namespace Storyblok.Client;
public abstract class StoryblokBaseClient
{
    protected readonly IMemoryCache MemoryCache;
    protected readonly ILogger<StoryblokBaseClient> Logger;
    protected readonly HttpClient Client;
    protected readonly StoryblokOptions Settings;
    internal static bool IsInEditor;

    protected StoryblokBaseClient(IOptions<StoryblokOptions> settings, IHttpClientFactory clientFactory, IMemoryCache memoryCache, ILogger<StoryblokBaseClient> logger)
    {
        Client = clientFactory.CreateClient();
        MemoryCache = memoryCache;
        Logger = logger;
        Settings = settings.Value;
        IsInEditor = true;

        ValidateSettings();
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(Settings.BaseUrl))
        {
            throw new Exception("Storyblok API URL is missing in app settings.");
        }

        if (IsInEditor && string.IsNullOrWhiteSpace(Settings.ApiKeyPreview))
        {
            throw new Exception("Storyblok preview API key is missing in app settings.");
        }

        if (!IsInEditor && string.IsNullOrWhiteSpace(Settings.ApiKeyPublic))
        {
            throw new Exception("Storyblok public API key is missing in app settings.");
        }

        if (Settings.CacheDurationSeconds < 0)
        {
            throw new Exception("Cache duration (in seconds) must be equal or greater than zero.");
        }

        if (Settings.SupportedCultures.Length == 0)
        {
            throw new Exception("Define at least one supported culture.");
        }
    }

    protected string ApiKey => Settings.IncludeDraftStories || IsInEditor ? Settings.ApiKeyPreview ?? string.Empty : Settings.ApiKeyPublic ?? string.Empty;

    protected bool IsDefaultCulture(CultureInfo culture)
    {
        return IsDefaultCulture(culture.ToString());
    }

    private bool IsDefaultCulture(string culture)
    {
        return Settings.SupportedCultures[0].Equals(culture, StringComparison.OrdinalIgnoreCase);
    }

    protected JsonSerializerOptions JsonOptions
    {
        get
        {
            JsonSerializerOptions options = new();
            options.Converters.Add(new StoryblokComponentConverter());
            options.Converters.Add(new StoryblokDateConverter());
            options.Converters.Add(new StoryblokNullableDateConverter());
            options.Converters.Add(new StoryblokIntConverter());
            options.Converters.Add(new StoryblokNullableIntConverter());
            options.Converters.Add(new StoryblokStringConverter());
            options.Converters.Add(new StoryblokNullableStringConverter());
            options.Converters.Add(new StoryblokMarkdownConverter());
            options.Converters.Add(new StoryblokAssetConverter());
            return options;
        }
    }
}
