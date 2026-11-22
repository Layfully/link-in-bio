using Client;
using Client.Data.Components;
using Client.Layout;
using Client.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Storyblok;
using Storyblok.Client;
using Storyblok.Extensions;

namespace Prerender;

/// <summary>Renders the home page with the app's own components, outside a browser.</summary>
public static class HomeRenderer
{
    /// <summary>Registers rendering services and records Storyblok responses for the browser.</summary>
    public static IServiceCollection AddPrerendering(this IServiceCollection services, IConfiguration storyblok)
    {
        services.AddLogging();
        services.AddSingleton(new PrerenderedStories { IsRecording = true });
        services.AddStoryblok(storyblok, typeof(App).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Site>();
        services.AddSingleton<NavigationManager, SiteNavigationManager>();
        services.AddSingleton<IJSRuntime, NoJavaScript>();
        services.AddSingleton<RenderErrors>();
        services.AddSingleton<IErrorBoundaryLogger>(provider => provider.GetRequiredService<RenderErrors>());
        return services;
    }

    /// <summary>Renders the published home page and collects its profile metadata and link warnings.</summary>
    public static async Task<RenderedHome> RenderAsync(IServiceProvider services)
    {
        StoryblokComponent[]? body = await LoadHomeBodyAsync(services);

        // Home reads the same cached story during rendering.
        await using HtmlRenderer renderer = new(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            ParameterView parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(RouteView.RouteData)] = new RouteData(typeof(Home), new Dictionary<string, object?>()),
                [nameof(RouteView.DefaultLayout)] = typeof(MainLayout),
            });
            HtmlRootComponent page = await renderer.RenderComponentAsync<RouteView>(parameters);
            return page.ToHtmlString();
        });

        // The layout catches component errors. Propagate them before publishing its fallback HTML.
        if (services.GetRequiredService<RenderErrors>().First is { } error)
        {
            throw new InvalidOperationException("The home page failed while rendering.", error);
        }

        StoryblokHeader? header = Home.Header(body);
        IReadOnlyList<StoryblokLink> links = Home.Links(body) ?? [];
        return new RenderedHome(html, ProfileOf(Home.SiteSettings(body), header, links), LinksWithoutAddress(links));
    }

    private static async Task<StoryblokComponent[]?> LoadHomeBodyAsync(IServiceProvider services)
    {
        StoryLoadResult<StoryblokPage> result = await services.GetRequiredService<StoryblokStoryClient>()
            .Story()
            .WithSlug(Home.Slug)
            .TryLoad<StoryblokPage>();

        return result switch
        {
            StoryblokStory<StoryblokPage> loaded when Home.HasContent(loaded.Content.Body) => loaded.Content.Body,
            StoryblokStory<StoryblokPage> => throw new InvalidOperationException("The home story has neither a header nor links."),
            StoryNotFound => throw new InvalidOperationException("The home story isn't published."),
            StoryblokUnavailable unavailable => throw new InvalidOperationException("Storyblok is unavailable.", unavailable.Error),
            StoryUnreadable unreadable => throw new InvalidOperationException("The home story can't be read.", unreadable.Error),
            null => throw new InvalidOperationException("The home story didn't load."),
        };
    }

    private static List<string> LinksWithoutAddress(IReadOnlyList<StoryblokLink> links)
    {
        List<string> labels = [];
        foreach (StoryblokLink link in links)
        {
            if (string.IsNullOrEmpty(link.Url) && !string.IsNullOrWhiteSpace(link.Label))
            {
                labels.Add(link.Label.Trim());
            }
        }

        return labels;
    }

    private static SiteProfile ProfileOf(StoryblokSiteSettings? settings, StoryblokHeader? header, IReadOnlyList<StoryblokLink> links)
    {
        StoryblokAsset? photo = settings?.SocialImage is { } socialImage && socialImage.IsSupported() ? socialImage : header?.Profile;
        string? image = photo is not null && photo.IsSupported()
            ? photo.Resize(SiteProfile.ImageSize, SiteProfile.ImageSize)
            : null;

        string[] profiles = [.. links.Select(link => link.Url)
            .Prepend(header?.ProfileLink?.Url)
            .OfType<string>()
            .Where(address => Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http")
            .Distinct(StringComparer.Ordinal)];

        return new SiteProfile(image, profiles)
        {
            Name = settings?.Name,
            JobTitle = settings?.JobTitle,
            Title = settings?.PageTitle,
            Description = settings?.Description,
            ImageAlt = string.IsNullOrWhiteSpace(photo?.Alt) ? settings?.Name : photo.Alt,
            CanonicalOrigin = settings?.CanonicalOrigin,
            AnalyticsMeasurementId = settings?.AnalyticsMeasurementId?.Trim(),
            AnalyticsHosts = settings?.AnalyticsHosts?.Trim(),
        };
    }

    private sealed class RenderErrors : IErrorBoundaryLogger
    {
        public Exception? First { get; private set; }

        public ValueTask LogErrorAsync(Exception exception)
        {
            First ??= exception;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SiteNavigationManager : NavigationManager
    {
        public SiteNavigationManager() => Initialize("https://prerender.invalid/", "https://prerender.invalid/");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            throw new NotSupportedException("Nothing navigates while the home page is prerendered.");
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("No JavaScript runs while the home page is prerendered.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("No JavaScript runs while the home page is prerendered.");
    }
}
