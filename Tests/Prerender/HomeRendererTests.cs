using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prerender;
using System.Net;

namespace LinkInBio.Tests.Prerender;

/// <summary>The home page rendered with the app's real components, against a fake Storyblok.</summary>
public sealed class HomeRendererTests : IDisposable
{
    private readonly FakeStoryblok _storyblok = new();

    public void Dispose() => _storyblok.Dispose();

    [Fact]
    public async Task The_rendered_home_page_shows_the_links_without_fading_in()
    {
        _storyblok.Answer(HttpStatusCode.OK, """
            { "story": { "name": "Home", "content": { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list", "entries": [
                    { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "GitHub", "css_class": "fab fa-github",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } }
                ] }
            ] } }, "cv": 0 }
            """);

        string? html = await HomeRenderer.RenderAsync(Services());

        Assert.NotNull(html);
        Assert.Contains("GitHub", html, StringComparison.Ordinal);
        Assert.Contains("https://github.com/Layfully", html, StringComparison.Ordinal);
        // Content that is in the HTML from the start doesn't fade in: Chrome doesn't count a photo first painted fully
        // transparent as the largest contentful paint, which then waits for Blazor's copy of it.
        Assert.DoesNotContain("reveal", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_home_page_that_isnt_published_isnt_rendered()
    {
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        Assert.Null(await HomeRenderer.RenderAsync(Services()));
    }

    private ServiceProvider Services()
    {
        IConfiguration storyblok = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ApiKeyPublic", "public-token")])
            .Build();

        // Requests go straight to the fake Storyblok, without the retry handler.
        return new ServiceCollection()
            .AddPrerendering(storyblok)
            .AddSingleton<IHttpClientFactory>(_storyblok)
            .BuildServiceProvider();
    }
}
