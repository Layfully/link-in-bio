using Bunit;
using Client;
using Client.Components;
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
        _context.Services.AddSingleton<Site>();
        _context.Services.AddSingleton(_prerendered);
        _context.Services.AddSingleton(new StoryblokStoryClient(
            Options.Create(new StoryblokOptions { ApiKeyPublic = "public-token" }),
            new StoryblokComponentTypes([typeof(App).Assembly]),
            _storyblok,
            _prerendered,
            TimeProvider.System,
            NullLogger<StoryblokStoryClient>.Instance));
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Certificate_ShouldDescribeImageByItsTitle_WhenAltTextIsEmpty()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Certificate("AWS Architect", alt: ""));

        // Act
        IRenderedComponent<Certificate> sut = RenderCertificate("aws-architect");

        // Assert
        AngleSharp.Dom.IElement image = sut.WaitForElement("figure img");
        image.GetAttribute("alt").ShouldBe("Certificate: AWS Architect");
        image.GetAttribute("src").ShouldBe("https://a.storyblok.com/f/1/2000x1414/abc/aws.jpg/m/1280x0");
        image.GetAttribute("width").ShouldBe("2000");
        image.GetAttribute("height").ShouldBe("1414");
    }

    [Fact]
    public void Certificate_ShouldKeepItsOwnAltText_WhenItHasOne()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Certificate("AWS Architect", alt: "AWS Solutions Architect certificate"));

        // Act
        IRenderedComponent<Certificate> sut = RenderCertificate("aws-architect");

        // Assert
        sut.WaitForElement("figure img").GetAttribute("alt").ShouldBe("AWS Solutions Architect certificate");
    }

    [Fact]
    public void Certificate_ShouldHandOverToTheNotFoundPage_WhenTheStoryIsMissing()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");
        bool notFound = false;
        _context.Services.GetRequiredService<NavigationManager>().OnNotFound += (_, _) => notFound = true;

        // Act
        RenderCertificate("nothing");

        // Assert
        notFound.ShouldBeTrue();
    }

    [Fact]
    public void Certificate_ShouldOfferToTryAgain_WhenStoryblokHasAnOutage()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.InternalServerError, "{}");

        // Act
        IRenderedComponent<Certificate> sut = RenderCertificate("aws-architect");

        // Assert
        sut.WaitForAssertion(() => sut.Find("button.action-button").TextContent.ShouldContain("Try again", Case.Sensitive));
        sut.FindAll("figure img").ShouldBeEmpty();
    }

    [Fact]
    public void Certificate_ShouldEscapeItsName_WhenAskingStoryblokForTheStory()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        // Act
        RenderCertificate("a b?c");

        // Assert
        _storyblok.Requests.Single().AbsolutePath.ShouldBe("/v2/cdn/stories/certificates/a%20b%3Fc");
    }

    [Fact]
    public void Home_ShouldListLinksAndLeaveOutEntriesWithoutALabel_WhenTheStoryHasLinks()
    {
        // Arrange
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

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.WaitForAssertion(() => sut.FindAll("nav li").Count.ShouldBe(2));
        AngleSharp.Dom.IElement link = sut.Find("nav li a");
        link.GetAttribute("href").ShouldBe("https://github.com/Layfully");
        link.TextContent.ShouldContain("GitHub", Case.Sensitive);
        sut.FindAll("nav li")[1].TextContent.ShouldContain("(not available)", Case.Sensitive); // no URL: plain text, not a link
    }

    [Fact]
    public void Home_ShouldCarryMarkersOnItsListAndEachLink_WhenTheBlocksAreEditable()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list",
                  "_editable": "<!--#storyblok#{\"uid\":\"list\",\"id\":\"7\"}-->", "entries": [
                    { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "GitHub",
                      "_editable": "<!--#storyblok#{\"uid\":\"github\",\"id\":\"7\"}-->",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } }
                ] }
            ] }
            """));

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.WaitForAssertion(() => sut.Find("nav").GetAttribute("data-blok-uid").ShouldBe("7-list"));
        sut.Find("nav li").GetAttribute("data-blok-uid").ShouldBe("7-github");
    }

    [Fact]
    public void Certificate_ShouldCarryItsMarker_WhenTheStoryIsEditable()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("AWS", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "certificate",
              "_editable": "<!--#storyblok#{\"uid\":\"aws\",\"id\":\"9\"}-->",
              "image": { "filename": "https://a.storyblok.com/f/1/2000x1414/abc/aws.jpg" } }
            """));

        // Act
        IRenderedComponent<Certificate> sut = RenderCertificate("aws");

        // Assert
        sut.WaitForAssertion(() => sut.Find("figure").GetAttribute("data-blok-uid").ShouldBe("9-aws"));
    }

    [Fact]
    public void Home_ShouldSayNothingIsPublishedYet_WhenTheHomePageIsntPublished()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.WaitForAssertion(() => sut.Markup.ShouldContain("Nothing here yet", Case.Sensitive));
        // Without a header the page still has its one heading, for screen readers and search engines.
        sut.FindAll("h1").ShouldHaveSingleItem().TextContent.ShouldBe("Links");
    }

    [Fact]
    public void Home_ShouldShowTheNameAsItsHeadingAndGiveTheLinksOneOfTheirOwn_WhenTheHeaderHasAName()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
                { "_uid": "55555555-5555-5555-5555-555555555555", "component": "site_settings", "display_name": "Example Person" },
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "header" },
                { "_uid": "33333333-3333-3333-3333-333333333333", "component": "list", "entries": [
                    { "_uid": "44444444-4444-4444-4444-444444444444", "component": "link", "label": "GitHub",
                      "url": { "linktype": "url", "url": "https://github.com/Layfully" } }
                ] }
            ] }
            """));

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.WaitForAssertion(() => sut.FindAll("header h1").ShouldHaveSingleItem());
        AngleSharp.Dom.IElement heading = sut.FindAll("h1").ShouldHaveSingleItem();
        heading.TextContent.ShouldBe("Example Person");
        (heading.ClassName ?? string.Empty).ShouldNotContain("sr-only", Case.Sensitive);
        sut.Find("nav h2").TextContent.ShouldBe("Profiles");
    }

    [Fact]
    public void Home_ShouldShowItsLinksInTheFirstRenderWithoutFadingIn_WhenItWasPrerendered()
    {
        // Arrange
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitHub"));

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.Find("nav li a").TextContent.ShouldContain("GitHub", Case.Sensitive);
        sut.FindAll(".reveal").ShouldBeEmpty();
    }

    [Fact]
    public void Home_ShouldReplaceThePrerenderedLinks_WhenTheHomePageHasChanged()
    {
        // Arrange
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitLab"));

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.WaitForAssertion(() => sut.Find("nav li a").TextContent.ShouldContain("GitLab", Case.Sensitive));
    }

    [Fact]
    public void Home_ShouldKeepThePrerenderedLinks_WhenTheRefreshFails()
    {
        // Arrange
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);

        // Act
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.WaitForAssertion(() => _storyblok.Requests.ShouldHaveSingleItem());
        sut.Find("nav li a").TextContent.ShouldContain("GitHub", Case.Sensitive);
        sut.Markup.ShouldNotContain("can't be loaded", Case.Sensitive);
    }

    [Fact]
    public void Home_ShouldShowTheNewestLinksAtOnceWithoutAskingAgain_WhenComingBackToIt()
    {
        // Arrange
        _prerendered.AddAll($$"""{ "home": {{HomeWithLink("GitHub")}} }""");
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitLab"));
        IRenderedComponent<Home> first = _context.Render<Home>();
        first.WaitForAssertion(() => first.Find("nav li a").TextContent.ShouldContain("GitLab", Case.Sensitive));

        // Act
        // Back on the page later in the same visit, such as after a certificate.
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Assert
        sut.Find("nav li a").TextContent.ShouldContain("GitLab", Case.Sensitive);
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public void Home_ShouldRerenderItsLinks_WhenTheHomeStoryIsEdited()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitHub"));
        IRenderedComponent<Home> sut = _context.Render<Home>();
        sut.WaitForAssertion(() => sut.Find("nav li a").TextContent.ShouldContain("GitHub", Case.Sensitive));

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory(EditedHome(HomeContentWithLink("GitLab"))));

        // Assert
        sut.WaitForAssertion(() => sut.Find("nav li a").TextContent.ShouldContain("GitLab", Case.Sensitive));
        sut.FindAll(".reveal").ShouldBeEmpty(); // an edit doesn't fade the page in again
    }

    [Fact]
    public void Home_ShouldSayNothingIsPublished_WhenAnEditRemovesEveryBlock()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitHub"));
        IRenderedComponent<Home> sut = _context.Render<Home>();
        sut.WaitForAssertion(() => sut.Markup.ShouldContain("GitHub", Case.Sensitive));

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory(EditedHome(
            """{ "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [] }""")));

        // Assert
        sut.WaitForAssertion(() => sut.Markup.ShouldContain("Nothing here yet", Case.Sensitive));
    }

    [Fact]
    public void Home_ShouldLeaveThePageAlone_WhenTheEditIsToAnotherStory()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitHub"));
        IRenderedComponent<Home> sut = _context.Render<Home>();
        sut.WaitForAssertion(() => sut.Markup.ShouldContain("GitHub", Case.Sensitive));

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory(EditedHome(HomeContentWithLink("GitLab"), fullSlug: "certificates/x")));

        // Assert
        sut.Find("nav li a").TextContent.ShouldContain("GitHub", Case.Sensitive);
    }

    [Fact]
    public void Certificate_ShouldRerender_WhenTheShownCertificateIsEdited()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        _storyblok.Answer(HttpStatusCode.OK, Certificate("AZ-204", "Azure"));
        IRenderedComponent<Certificate> sut = RenderCertificate("AZ-204");
        sut.WaitForElement("figure img");

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory($$"""
            { "name": "AZ-204", "full_slug": "certificates/az-204", "content":
              { "_uid": "11111111-1111-1111-1111-111111111111", "component": "certificate",
                "image": { "filename": "https://a.storyblok.com/f/1/2000x1414/def/new.jpg", "alt": "New scan" } } }
            """));

        // Assert
        sut.WaitForAssertion(() => sut.Find("figure img").GetAttribute("alt").ShouldBe("New scan"));
    }

    [Fact]
    public void Home_ShouldKeepTheEdit_WhenALoadFinishesAfterIt()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        TaskCompletionSource release = _storyblok.AnswerLater(HttpStatusCode.OK, HomeWithLink("Saved"));
        IRenderedComponent<Home> sut = _context.Render<Home>();

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory(EditedHome(HomeContentWithLink("Typed"))));
        sut.WaitForAssertion(() => sut.Find("nav li a").TextContent.ShouldContain("Typed", Case.Sensitive));
        release.SetResult();

        // Assert
        // The load ends with the saved draft; the page keeps what is being typed.
        Should.Throw<Bunit.Extensions.WaitForHelpers.WaitForFailedException>(() =>
            sut.WaitForAssertion(() => sut.Markup.ShouldContain("Saved", Case.Sensitive), TimeSpan.FromMilliseconds(500)));
        sut.Find("nav li a").TextContent.ShouldContain("Typed", Case.Sensitive);
    }

    [Fact]
    public void Certificate_ShouldKeepTheEdit_WhenALoadFinishesAfterIt()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        TaskCompletionSource release = _storyblok.AnswerLater(HttpStatusCode.NotFound);
        bool notFound = false;
        _context.Services.GetRequiredService<NavigationManager>().OnNotFound += (_, _) => notFound = true;
        IRenderedComponent<Certificate> sut = RenderCertificate("az-204");

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory(EditedCertificate("az-204", "New scan")));
        sut.WaitForAssertion(() => sut.Find("figure img").GetAttribute("alt").ShouldBe("New scan"));
        release.SetResult();

        // Assert
        Should.Throw<Bunit.Extensions.WaitForHelpers.WaitForFailedException>(() =>
            sut.WaitForAssertion(() => notFound.ShouldBeTrue(), TimeSpan.FromMilliseconds(500)));
        sut.Find("figure img").GetAttribute("alt").ShouldBe("New scan");
    }

    [Fact]
    public void Certificate_ShouldStayInThePreview_WhenAnEditRemovesItsImage()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        _storyblok.Answer(HttpStatusCode.OK, Certificate("az-204", "Azure"));
        bool notFound = false;
        _context.Services.GetRequiredService<NavigationManager>().OnNotFound += (_, _) => notFound = true;
        IRenderedComponent<Certificate> sut = RenderCertificate("az-204");
        sut.WaitForElement("figure img");

        // Act
        sut.InvokeAsync(() => client.ReadEditedStory("""
            { "name": "az-204", "full_slug": "certificates/az-204", "content":
              { "_uid": "11111111-1111-1111-1111-111111111111", "component": "certificate" } }
            """));

        // Assert
        sut.WaitForAssertion(() => sut.FindAll("figure").ShouldBeEmpty());
        notFound.ShouldBeFalse();
    }

    private static string EditedCertificate(string slugName, string alt) => $$"""
        { "name": "{{slugName}}", "full_slug": "certificates/{{slugName}}", "content":
          { "_uid": "11111111-1111-1111-1111-111111111111", "component": "certificate",
            "image": { "filename": "https://a.storyblok.com/f/1/2000x1414/def/new.jpg", "alt": "{{alt}}" } } }
        """;

    [Fact]
    public async Task Home_ShouldIgnoreEdits_WhenThePageWasLeft()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        _storyblok.Answer(HttpStatusCode.OK, HomeWithLink("GitHub"));
        IRenderedComponent<Home> sut = _context.Render<Home>();
        sut.WaitForAssertion(() => sut.Markup.ShouldContain("GitHub", Case.Sensitive));

        // Act
        await _context.DisposeComponentsAsync();

        // Assert
        // Read through reflection: a page that stayed subscribed would be kept alive and re-rendered after it's gone.
        (typeof(StoryblokStoryClient)
            .GetField(nameof(StoryblokStoryClient.StoryEdited), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(client)).ShouldBeNull();
    }

    [Fact]
    public void StoryblokBridge_ShouldHandEditsToTheClient_WhenAnEditArrives()
    {
        // Arrange
        StoryblokStoryClient client = UseEditor();
        List<StoryblokEdit> edits = [];
        client.StoryEdited += edits.Add;
        IRenderedComponent<StoryblokBridge> sut = _context.Render<StoryblokBridge>();

        // Act
        sut.Instance.ReadEditedStory(EditedHome(HomeContentWithLink("GitLab")));

        // Assert
        edits.ShouldHaveSingleItem().TryGet("/home", out StoryblokStory<StoryblokPage>? _).ShouldBeTrue();
    }

    // Replaces the published client with one in the Visual Editor. Call before the first render.
    private StoryblokStoryClient UseEditor()
    {
        StoryblokOptions options = new() { ApiKeyPublic = "public-token", ApiKeyPreview = "preview-token", IsInEditor = true };
        StoryblokStoryClient client = new(
            Options.Create(options),
            new StoryblokComponentTypes([typeof(App).Assembly]),
            _storyblok,
            _prerendered,
            TimeProvider.System,
            NullLogger<StoryblokStoryClient>.Instance);
        _context.Services.AddSingleton(client);
        _context.Services.AddSingleton(Options.Create(options));
        return client;
    }

    private static string EditedHome(string content, string fullSlug = "home") =>
        $$"""{ "name": "Home", "full_slug": "{{fullSlug}}", "content": {{content}} }""";

    private static string HomeContentWithLink(string label) => $$"""
        { "_uid": "11111111-1111-1111-1111-111111111111", "component": "page", "body": [
            { "_uid": "22222222-2222-2222-2222-222222222222", "component": "list", "entries": [
                { "_uid": "33333333-3333-3333-3333-333333333333", "component": "link", "label": "{{label}}",
                  "url": { "linktype": "url", "url": "https://example.com" } }
            ] }
        ] }
        """;

    private static string HomeWithLink(string label) => Story("Home", HomeContentWithLink(label));

    private IRenderedComponent<Certificate> RenderCertificate(string name) =>
        _context.Render<Certificate>(parameters => parameters.Add(page => page.Name, name));

    private static string Certificate(string name, string alt) => Story(name, $$"""
        { "_uid": "11111111-1111-1111-1111-111111111111", "component": "certificate",
          "image": { "filename": "https://a.storyblok.com/f/1/2000x1414/abc/aws.jpg", "alt": "{{alt}}" } }
        """);

    private static string Story(string name, string content) =>
        $$"""{ "story": { "name": "{{name}}", "content": {{content}} }, "cv": 0 }""";
}
