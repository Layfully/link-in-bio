using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storyblok.Client;

namespace Storyblok.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStoryblok(this IServiceCollection services, IConfigurationSection? configurationSection, Action<StoryblokOptions>? configure = null)
    {
        services.Configure<StoryblokOptions>(storyblokOptions =>
        {
            configurationSection?.Bind(storyblokOptions);
            configure?.Invoke(storyblokOptions);
        });

        return AddStoryblok(services);
    }

    public static IServiceCollection AddStoryblok(this IServiceCollection services, Action<StoryblokOptions>? configure)
    {
        return AddStoryblok(services, null, configure);
    }
    
    internal const string HttpClientName = "Storyblok";

    private static IServiceCollection AddStoryblok(this IServiceCollection services)
    {
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
