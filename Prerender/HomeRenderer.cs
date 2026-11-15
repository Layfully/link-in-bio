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
    /// <summary>
    /// The services the app's components need when rendered here. The Storyblok client records the responses it loads
    /// (<see cref="PrerenderedStories.IsRecording"/>), for the page to start from.
    /// </summary>
    public static IServiceCollection AddPrerendering(this IServiceCollection services, IConfiguration storyblok)
    {
        services.AddLogging();
        services.AddSingleton(new PrerenderedStories { IsRecording = true });
        services.AddStoryblok(storyblok, typeof(App).Assembly);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<NavigationManager>(new SiteNavigationManager());
        services.AddSingleton<IJSRuntime, NoJavaScript>();
        services.AddSingleton<RenderErrors>();
        services.AddSingleton<IErrorBoundaryLogger>(provider => provider.GetRequiredService<RenderErrors>());
        return services;
    }

    /// <summary>
    /// What the app renders into <c>#app</c> for <c>/</c>: the home page in the layout, and the profile it shows for the
    /// page's metadata. Throws, saying why, unless the home story loads with something to show: a page that would say
    /// "not found", "nothing here yet" or "can't be loaded" is not worth putting into the HTML, the app says that itself.
    /// </summary>
    public static async Task<RenderedHome> RenderAsync(IServiceProvider services)
    {
        StoryLoadResult<StoryblokPage> result = await services.GetRequiredService<StoryblokStoryClient>()
            .Story()
            .WithSlug("/home")
            .TryLoad<StoryblokPage>();

        StoryblokStory<StoryblokPage> story = result switch
        {
            StoryblokStory<StoryblokPage> loaded when Home.HasContent(loaded.Content.Body) => loaded,
            StoryblokStory<StoryblokPage> => throw new InvalidOperationException("The home story has neither a header nor links."),
            StoryNotFound => throw new InvalidOperationException("The home story isn't published."),
            StoryblokUnavailable unavailable => throw new InvalidOperationException("Storyblok is unavailable.", unavailable.Error),
            StoryUnreadable unreadable => throw new InvalidOperationException("The home story can't be read.", unreadable.Error),
            null => throw new InvalidOperationException("The home story didn't load."),
        };

        // The page loads the story again while rendering; it comes from the cache, the same response that was recorded.
        await using HtmlRenderer renderer = new(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent page = await renderer.RenderComponentAsync<RouteView>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(RouteView.RouteData)] = new RouteData(typeof(Home), new Dictionary<string, object?>()),
                [nameof(RouteView.DefaultLayout)] = typeof(MainLayout),
            }));
            return page.ToHtmlString();
        });

        // The layout's error boundary turns an exception into its "Something went wrong" message, which must not become
        // the page every visitor sees first.
        if (services.GetRequiredService<RenderErrors>().First is { } error)
        {
            throw new InvalidOperationException("The home page failed while rendering.", error);
        }

        return new RenderedHome(html, ProfileOf(story.Content.Body), LinksWithoutAddress(story.Content.Body));
    }

    // The list shows such an entry as plain text, which is easy to miss after a typo in Storyblok: an address without
    // https:// is refused, as is any other scheme, and the entry quietly stops being a link. Entries without a label
    // aren't shown at all.
    private static string[] LinksWithoutAddress(StoryblokComponent[]? body) =>
        [.. (Home.Links(body) ?? []).Where(link => !string.IsNullOrWhiteSpace(link.Label) && string.IsNullOrEmpty(link.Url)).Select(link => link.Label!.Trim())];

    // What the page shows about the person, as the page's metadata wants it: the photo cut square for link previews
    // (og:image says 600 x 600), and every profile the page links to as an absolute web address.
    private static SiteProfile ProfileOf(StoryblokComponent[]? body)
    {
        StoryblokHeader? header = Home.Header(body);
        string? image = header?.Profile is { } photo && photo.IsSupported() ? photo.Resize(600, 600) : null;

        IEnumerable<string?> addresses = [header?.ProfileLink?.Url, .. (Home.Links(body) ?? []).Select(link => link.Url)];
        string[] profiles = [.. addresses
            .Where(address => Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http")
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)];

        return new SiteProfile(image, profiles);
    }

    // Takes the place of the error boundary's logger to notice that the page failed.
    private sealed class RenderErrors : IErrorBoundaryLogger
    {
        public Exception? First { get; private set; }

        public ValueTask LogErrorAsync(Exception exception)
        {
            First ??= exception;
            return ValueTask.CompletedTask;
        }
    }

    // The address the page is rendered for. Components only read it; nothing navigates while rendering.
    private sealed class SiteNavigationManager : NavigationManager
    {
        public SiteNavigationManager() => Initialize($"{Site.Url}/", $"{Site.Url}/");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            throw new NotSupportedException("Nothing navigates while the home page is prerendered.");
    }

    // Components start their JavaScript after rendering in a browser (OnAfterRender), which never happens here.
    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("No JavaScript runs while the home page is prerendered.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("No JavaScript runs while the home page is prerendered.");
    }
}
