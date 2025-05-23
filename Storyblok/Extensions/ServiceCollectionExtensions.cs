﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storyblok.Client;

namespace Storyblok.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStoryblok(this IServiceCollection services, IConfigurationSection? configurationSection, Action<StoryblokOptions>? configure = null)
    {
        StoryblokOptions options = new();

        configurationSection?.Bind(options);

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
    
    private static IServiceCollection AddStoryblok(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<StoryblokStoryClient>();
        services.AddMemoryCache();
        return services;
    }
}
