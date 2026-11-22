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
    /// <summary>The service key for Storyblok's HTTP handler.</summary>
    public const string HttpHandlerKey = "Storyblok";

    /// <summary>
    /// Registers the Storyblok client and discovers classes marked with <see cref="StoryblokComponentAttribute"/>.
    /// </summary>
    public static IServiceCollection AddStoryblok(this IServiceCollection services, IConfiguration configuration, params Assembly[] componentAssemblies)
    {
        // Validate after the app has configured editor mode.
        services.AddOptions<StoryblokOptions>()
            .Configure(options => Read(configuration, options))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "Storyblok:BaseUrl is missing.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKeyPublic),
                "Storyblok:ApiKeyPublic is missing. For local development, put it in wwwroot/appsettings.Development.json.")
            .Validate(options => !options.IsInEditor || !string.IsNullOrWhiteSpace(options.ApiKeyPreview),
                "Storyblok:ApiKeyPreview is missing, but the site runs in the Visual Editor.")
            .Validate(options => options.CacheDurationSeconds >= 0, "Storyblok:CacheDurationSeconds can't be negative.");

        services.AddSingleton(_ => new StoryblokComponentTypes(componentAssemblies));

        services.TryAddKeyedSingleton<HttpMessageHandler>(HttpHandlerKey, (provider, _) =>
            new StoryblokRetryHandler(RetryPolicy.Default, provider.GetRequiredService<TimeProvider>())
            {
                InnerHandler = new HttpClientHandler(),
            });
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<StoryblokStoryClient>();
        services.TryAddSingleton<PrerenderedStories>();
        return services;
    }

    // Read settings directly to avoid adding configuration binder assemblies to the client.
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
