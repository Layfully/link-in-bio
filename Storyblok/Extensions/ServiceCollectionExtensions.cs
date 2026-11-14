using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Storyblok.Attributes;
using Storyblok.Client;
using System.Globalization;
using System.Reflection;

namespace Storyblok.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>The key of the <see cref="HttpMessageHandler"/> that sends Storyblok's requests; tests register a fake under it.</summary>
    public const string HttpHandlerKey = "Storyblok";

    /// <summary>
    /// Registers <see cref="StoryblokStoryClient"/>, configured from <paramref name="configuration"/>. Storyblok components
    /// are read into the classes marked with <see cref="StoryblokComponentAttribute"/> in <paramref name="componentAssemblies"/>.
    /// </summary>
    public static IServiceCollection AddStoryblok(this IServiceCollection services, IConfiguration configuration, params Assembly[] componentAssemblies)
    {
        // Checked when the options are first read, after every Configure call (the app decides IsInEditor in its own).
        services.AddOptions<StoryblokOptions>()
            .Configure(options => Read(configuration, options))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "Storyblok:BaseUrl is missing.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKeyPublic),
                "Storyblok:ApiKeyPublic is missing. For local development, put it in wwwroot/appsettings.Development.json.")
            .Validate(options => !options.IsInEditor || !string.IsNullOrWhiteSpace(options.ApiKeyPreview),
                "Storyblok:ApiKeyPreview is missing, but the site runs in the Visual Editor.")
            .Validate(options => options.CacheDurationSeconds >= 0, "Storyblok:CacheDurationSeconds can't be negative.");

        services.AddSingleton(_ => new StoryblokComponentTypes(componentAssemblies));

        // Retries transient failures (network errors, 408, 429, 5xx) and caps how long a page waits when Storyblok hangs.
        // A handler of its own rather than IHttpClientFactory: the site sends one kind of request, and the factory is
        // three more assemblies for every visitor to download and load.
        services.TryAddKeyedSingleton<HttpMessageHandler>(HttpHandlerKey, (_, _) => new StoryblokRetryHandler { InnerHandler = new HttpClientHandler() });
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<StoryblokStoryClient>();
        services.TryAddSingleton<PrerenderedStories>();
        return services;
    }

    // Read by hand rather than with Microsoft.Extensions.Configuration.Binder, which together with
    // System.ComponentModel.TypeConverter is two more assemblies every visitor downloads and loads, for four settings.
    // A setting that is left out keeps its default; IsInEditor is the app's to decide, not configuration's.
    private static void Read(IConfiguration configuration, StoryblokOptions options)
    {
        options.ApiKeyPublic = configuration[nameof(StoryblokOptions.ApiKeyPublic)] ?? options.ApiKeyPublic;
        options.ApiKeyPreview = configuration[nameof(StoryblokOptions.ApiKeyPreview)] ?? options.ApiKeyPreview;
        options.BaseUrl = configuration[nameof(StoryblokOptions.BaseUrl)] ?? options.BaseUrl;

        if (configuration[nameof(StoryblokOptions.CacheDurationSeconds)] is { } cacheDuration)
        {
            options.CacheDurationSeconds = int.TryParse(cacheDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
                ? seconds
                : throw new InvalidOperationException($"Storyblok:CacheDurationSeconds must be a whole number of seconds, not \"{cacheDuration}\".");
        }
    }
}
