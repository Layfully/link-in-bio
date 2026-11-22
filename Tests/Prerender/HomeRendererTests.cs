using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prerender;
using Storyblok.Extensions;
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

        string html = (await RenderHomeAsync()).Html;

        Assert.Contains("GitHub", html, StringComparison.Ordinal);
        Assert.Contains("https://github.com/Layfully", html, StringComparison.Ordinal);
        // Content that is in the HTML from the start doesn't fade in: Chrome doesn't count a photo first painted fully
        // transparent as the largest contentful paint, which then waits for Blazor's copy of it.
        Assert.DoesNotContain("reveal", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_profile_is_the_photo_cut_square_and_every_web_address_the_page_links_to()
    {
        _storyblok.Answer(HttpStatusCode.OK, """
            { "story": { "name": "Home", "content": { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "99999999-9999-9999-9999-999999999999", "component": "site_settings",
                  "display_name": "Example Person", "professional_title": "Designer", "seo_description": "Example profile",
                  "canonical_url": "https://profile.example/", "analytics_measurement_id": "G-EXAMPLE123",
                  "analytics_hosts": "profile.example,www.profile.example" },
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "header",
                  "profile": { "filename": "https://a.storyblok.com/f/1/608x912/abc/profile.jpg" },
                  "header_link": [ { "_uid": "66666666-6666-6666-6666-666666666666", "component": "link", "label": "Portfolio",
                                     "url": { "linktype": "url", "url": "https://portfolio.example.com" } } ] },
                { "_uid": "33333333-3333-3333-3333-333333333333", "component": "list", "entries": [
                    { "_uid": "44444444-4444-4444-4444-444444444444", "component": "link", "label": "GitHub",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } },
                    { "_uid": "55555555-5555-5555-5555-555555555555", "component": "link", "label": "Mail",
                      "url": { "linktype": "email", "email": "me@example.com" } },
                    { "_uid": "77777777-7777-7777-7777-777777777777", "component": "link", "label": "Certificate",
                      "url": { "linktype": "story", "cached_url": "certificates/aws" } },
                    { "_uid": "88888888-8888-8888-8888-888888888888", "component": "link", "label": "Portfolio again",
                      "url": { "linktype": "url", "url": "https://portfolio.example.com" } }
                ] }
            ] } }, "cv": 0 }
            """);

        RenderedHome home = await RenderHomeAsync();
        SiteProfile profile = home.Profile;

        Assert.Contains("Example Person</h1>", home.Html, StringComparison.Ordinal);
        Assert.Equal("Example Person - Designer", profile.Title);
        Assert.Equal("Example profile", profile.Description);
        Assert.Equal("https://profile.example", profile.CanonicalOrigin);
        Assert.Equal("G-EXAMPLE123", profile.AnalyticsMeasurementId);
        Assert.Equal("profile.example,www.profile.example", profile.AnalyticsHosts);
        Assert.Equal("https://a.storyblok.com/f/1/608x912/abc/profile.jpg/m/600x600/smart", profile.Image);
        Assert.Equal(["https://portfolio.example.com", "https://github.com/Layfully"], profile.SameAs);
    }

    [Fact]
    public async Task Listed_links_whose_address_the_site_refused_or_lacks_are_named()
    {
        _storyblok.Answer(HttpStatusCode.OK, """
            { "story": { "name": "Home", "content": { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list", "entries": [
                    { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "GitHub",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } },
                    { "_uid": "44444444-4444-4444-4444-444444444444", "component": "link", "label": "Typo",
                      "url": { "linktype": "url", "url": "www.example.com" } },
                    { "_uid": "55555555-5555-5555-5555-555555555555", "component": "link", "label": " Coming soon ",
                      "url": { "linktype": "url", "url": "" } },
                    { "_uid": "66666666-6666-6666-6666-666666666666", "component": "link", "label": "",
                      "url": { "linktype": "url", "url": "" } }
                ] }
            ] } }, "cv": 0 }
            """);

        RenderedHome home = await RenderHomeAsync();

        Assert.Equal(["Typo", "Coming soon"], home.LinksWithoutAddress);
    }

    [Fact]
    public async Task A_home_page_that_isnt_published_isnt_rendered()
    {
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(RenderHomeAsync);

        Assert.Equal("The home story isn't published.", exception.Message);
    }

    [Fact]
    public async Task An_outage_says_what_Storyblok_answered()
    {
        _storyblok.Answer(HttpStatusCode.Unauthorized, "{}");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(RenderHomeAsync);

        Assert.Equal("Storyblok is unavailable.", exception.Message);
        Assert.Contains("401", ExceptionText.Describe(exception), StringComparison.Ordinal);
    }

    private async Task<RenderedHome> RenderHomeAsync()
    {
        IConfiguration storyblok = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ApiKeyPublic", "public-token")])
            .Build();

        // Requests go straight to the fake Storyblok, without the retry handler.
        await using ServiceProvider services = new ServiceCollection()
            .AddPrerendering(storyblok)
            .AddKeyedSingleton<HttpMessageHandler>(ServiceCollectionExtensions.HttpHandlerKey, _storyblok)
            .BuildServiceProvider();
        return await HomeRenderer.RenderAsync(services);
    }
}
