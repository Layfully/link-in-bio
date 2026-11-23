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
    public async Task RenderAsync_ShouldShowTheLinksWithoutFadingIn_WhenTheHomeStoryHasAList()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, """
            { "story": { "name": "Home", "content": { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list", "entries": [
                    { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "GitHub", "css_class": "fab fa-github",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } }
                ] }
            ] } }, "cv": 0 }
            """);

        // Act
        string html = (await RenderHomeAsync()).Html;

        // Assert
        html.ShouldContain("GitHub", Case.Sensitive);
        html.ShouldContain("https://github.com/Layfully", Case.Sensitive);
        // Content that is in the HTML from the start doesn't fade in: Chrome doesn't count a photo first painted fully
        // transparent as the largest contentful paint, which then waits for Blazor's copy of it.
        html.ShouldNotContain("reveal", Case.Sensitive);
    }

    [Fact]
    public async Task RenderAsync_ShouldCutThePhotoSquareAndCollectEveryWebAddress_WhenTheStoryHasProfileSettings()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, """
            { "story": { "name": "Home", "content": { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "99999999-9999-9999-9999-999999999999", "component": "site_settings",
                  "display_name": "Example Person", "professional_title": "Designer", "seo_description": "Example profile",
                  "canonical_url": "https://profile.example/", "analytics_measurement_id": "G-EXAMPLE123",
                  "analytics_hosts": "profile.example,www.profile.example",
                  "app_name": " Example Links ",
                  "app_icon": { "filename": "https://a.storyblok.com/f/1/512x512/example/icon.png" },
                  "app_maskable_icon": { "filename": "https://other.example/icon.png" } },
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

        // Act
        RenderedHome home = await RenderHomeAsync();
        SiteProfile profile = home.Profile;

        // Assert
        home.Html.ShouldContain("Example Person</h1>", Case.Sensitive);
        profile.Title.ShouldBe("Example Person - Designer");
        profile.Description.ShouldBe("Example profile");
        profile.CanonicalOrigin.ShouldBe("https://profile.example");
        profile.AnalyticsMeasurementId.ShouldBe("G-EXAMPLE123");
        profile.AnalyticsHosts.ShouldBe("profile.example,www.profile.example");
        profile.AppName.ShouldBe("Example Links");
        profile.AppIcon.ShouldBe("https://a.storyblok.com/f/1/512x512/example/icon.png");
        profile.AppMaskableIcon.ShouldBeNull();
        profile.Image.ShouldBe("https://a.storyblok.com/f/1/608x912/abc/profile.jpg/m/600x600/smart");
        profile.SameAs.ShouldBe(["https://portfolio.example.com", "https://github.com/Layfully"]);
    }

    [Fact]
    public async Task RenderAsync_ShouldNameLinksWithoutAnAddress_WhenTheSiteRefusedOrLacksTheUrl()
    {
        // Arrange
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

        // Act
        RenderedHome home = await RenderHomeAsync();

        // Assert
        home.LinksWithoutAddress.ShouldBe(["Typo", "Coming soon"]);
    }

    [Fact]
    public async Task RenderAsync_ShouldThrow_WhenTheHomeStoryIsNotPublished()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(RenderHomeAsync);

        // Assert
        exception.Message.ShouldBe("The home story isn't published.");
    }

    [Fact]
    public async Task RenderAsync_ShouldSayWhatStoryblokAnswered_WhenTheApiIsUnavailable()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.Unauthorized, "{}");

        // Act
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(RenderHomeAsync);

        // Assert
        exception.Message.ShouldBe("Storyblok is unavailable.");
        ExceptionText.Describe(exception).ShouldContain("401", Case.Sensitive);
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
