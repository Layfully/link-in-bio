using Bunit;
using Client;
using Client.Data.Components;
using Client.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Storyblok;
using Storyblok.Client;
using System.Net;

namespace LinkInBio.Tests.Client;

/// <summary>
/// What the pages show for each answer from Storyblok. The pages render with the real Storyblok client and the app's
/// component classes, against a fake Storyblok; JavaScript effects are ignored (loose JS interop).
/// </summary>
public sealed class PageTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly FakeStoryblok _storyblok = new();
    private readonly PrerenderedStories _prerendered = new();

    public PageTests()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton(_prerendered);
        _context.Services.AddSingleton(new StoryblokStoryClient(
            Options.Create(new StoryblokOptions { ApiKeyPublic = "public-token" }),
            new StoryblokComponentTypes([typeof(App).Assembly], ComponentJson.Default),
            _storyblok,
            _prerendered,
            TimeProvider.System,
            NullLogger<StoryblokStoryClient>.Instance));
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void A_certificate_without_alt_text_is_described_by_its_title()
    {
        _storyblok.Answer(HttpStatusCode.OK, Certificate("AWS Architect", alt: ""));

        IRenderedComponent<Certificate> page = RenderCertificate("aws-architect");

        AngleSharp.Dom.IElement image = page.WaitForElement("figure img");
        Assert.Equal("Certificate: AWS Architect", image.GetAttribute("alt"));
        Assert.Equal("https://a.storyblok.com/f/1/2000x1414/abc/aws.jpg/m/1280x0", image.GetAttribute("src"));
        Assert.Equal("2000", image.GetAttribute("width"));
        Assert.Equal("1414", image.GetAttribute("height"));
    }

    [Fact]
    public void A_certificates_own_alt_text_is_kept()
    {
        _storyblok.Answer(HttpStatusCode.OK, Certificate("AWS Architect", alt: "AWS Solutions Architect certificate"));

        IRenderedComponent<Certificate> page = RenderCertificate("aws-architect");

        Assert.Equal("AWS Solutions Architect certificate", page.WaitForElement("figure img").GetAttribute("alt"));
    }

    [Fact]
    public void A_missing_certificate_hands_over_to_the_not_found_page()
    {
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");
        bool notFound = false;
        _context.Services.GetRequiredService<NavigationManager>().OnNotFound += (_, _) => notFound = true;

        RenderCertificate("nothing");

        Assert.True(notFound);
    }

    [Fact]
    public void An_outage_offers_to_try_again()
    {
        _storyblok.Answer(HttpStatusCode.InternalServerError, "{}");

        IRenderedComponent<Certificate> page = RenderCertificate("aws-architect");

        page.WaitForAssertion(() => Assert.Contains("Try again", page.Find("button.action-button").TextContent));
        Assert.Empty(page.FindAll("figure img"));
    }

    [Fact]
    public void The_certificate_name_is_escaped_before_it_reaches_Storyblok()
    {
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        RenderCertificate("a b?c");

        Assert.Equal("/v2/cdn/stories/certificates/a%20b%3Fc", _storyblok.Requests.Single().AbsolutePath);
    }

    [Fact]
    public void The_home_page_lists_the_links_and_leaves_out_entries_without_a_label()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list", "entries": [
                    { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "GitHub", "css_class": "fab fa-github",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } },
                    { "_uid": "44444444-4444-4444-4444-444444444444", "component": "link", "label": "Coming soon", "url": { "linktype": "url", "url": "" } },
                    { "_uid": "55555555-5555-5555-5555-555555555555", "component": "link", "label": "", "url": { "linktype": "url", "url": "https://example.com" } }
                ] }
            ] }
            """));

        IRenderedComponent<Home> page = _context.Render<Home>();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("nav li").Count));
        AngleSharp.Dom.IElement link = page.Find("nav li a");
        Assert.Equal("https://github.com/Layfully", link.GetAttribute("href"));
        Assert.Contains("GitHub", link.TextContent);
        Assert.Contains("(not available)", page.FindAll("nav li")[1].TextContent); // no URL: plain text, not a link
    }

    [Fact]
    public void A_home_page_that_isnt_published_says_so()
    {
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        IRenderedComponent<Home> page = _context.Render<Home>();

        page.WaitForAssertion(() => Assert.Contains("Nothing here yet", page.Markup));
    }

    [Fact]
    public void A_prerendered_home_page_shows_its_links_in_the_first_render_without_fading_in()
    {
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitHub"));

        IRenderedComponent<Home> page = _context.Render<Home>();

        Assert.Contains("GitHub", page.Find("nav li a").TextContent);
        Assert.Empty(page.FindAll(".reveal"));
    }

    [Fact]
    public void A_changed_home_page_replaces_the_prerendered_one()
    {
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitLab"));

        IRenderedComponent<Home> page = _context.Render<Home>();

        page.WaitForAssertion(() => Assert.Contains("GitLab", page.Find("nav li a").TextContent));
    }

    [Fact]
    public void A_failed_refresh_keeps_the_prerendered_links()
    {
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);

        IRenderedComponent<Home> page = _context.Render<Home>();

        page.WaitForAssertion(() => Assert.Single(_storyblok.Requests));
        Assert.Contains("GitHub", page.Find("nav li a").TextContent);
        Assert.DoesNotContain("can't be loaded", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Coming_back_to_the_home_page_shows_the_newest_links_at_once_without_asking_again()
    {
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitLab"));
        IRenderedComponent<Home> first = _context.Render<Home>();
        first.WaitForAssertion(() => Assert.Contains("GitLab", first.Find("nav li a").TextContent));

        // Back on the page later in the same visit, such as after a certificate.
        IRenderedComponent<Home> again = _context.Render<Home>();

        Assert.Contains("GitLab", again.Find("nav li a").TextContent);
        Assert.Single(_storyblok.Requests);
    }

    private static string HomeWithLink(string label) => Story("Home", $$"""
        { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
            { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list", "entries": [
                { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "{{label}}",
                  "url": { "linktype": "url", "url": "https://example.com" } }
            ] }
        ] }
        """);

    private IRenderedComponent<Certificate> RenderCertificate(string name) =>
        _context.Render<Certificate>(parameters => parameters.Add(page => page.Name, name));

    private static string Certificate(string name, string alt) => Story(name, $$"""
        { "_uid": "11111111-1111-1111-1111-111111111111", "component": "certificate",
          "image": { "filename": "https://a.storyblok.com/f/1/2000x1414/abc/aws.jpg", "alt": "{{alt}}" } }
        """);

    private static string Story(string name, string content) =>
        $$"""{ "story": { "name": "{{name}}", "content": {{content}} }, "cv": 0 }""";
}
