using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storyblok.Attributes;
using Storyblok.Client;
using System.Reflection;

namespace Storyblok.Extensions;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "Storyblok";

    /// <summary>
    /// Registers <see cref="StoryblokStoryClient"/>, configured from <paramref name="configuration"/>. Storyblok components
    /// are read into the classes marked with <see cref="StoryblokComponentAttribute"/> in <paramref name="componentAssemblies"/>.
    /// </summary>
    public static IServiceCollection AddStoryblok(this IServiceCollection services, IConfiguration configuration, params Assembly[] componentAssemblies)
    {
        // Checked when the options are first read, after every Configure call (the app decides IsInEditor in its own).
        services.AddOptions<StoryblokOptions>()
            .Configure(options => configuration.Bind(options))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "Storyblok:BaseUrl is missing.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKeyPublic),
                "Storyblok:ApiKeyPublic is missing. For local development, put it in wwwroot/appsettings.Development.json.")
            .Validate(options => !options.IsInEditor || !string.IsNullOrWhiteSpace(options.ApiKeyPreview),
                "Storyblok:ApiKeyPreview is missing, but the site runs in the Visual Editor.")
            .Validate(options => options.CacheDurationSeconds >= 0, "Storyblok:CacheDurationSeconds can't be negative.");

        services.AddSingleton(_ => new StoryblokComponentTypes(componentAssemblies));

        // Retries transient failures (network errors, 408, 429, 5xx) and caps how long a page waits when Storyblok hangs.
        services.AddHttpClient(HttpClientName)
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.Retry.MaxRetryAttempts = 2;
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            });
        services.AddScoped<StoryblokStoryClient>();
        services.AddMemoryCache();
        return services;
    }
}
