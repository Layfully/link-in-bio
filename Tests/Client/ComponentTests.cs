using Bunit;
using Client.Components;
using Client.Data.Components;
using Client.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Storyblok;
using Storyblok.Client;

namespace LinkInBio.Tests.Client;

/// <summary>The loading hints while content loads, and the layout's message for a page that fails.</summary>
public sealed class ComponentTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly FakeTimeProvider _time = new();
    private readonly FakeStoryblok _storyblok = new();

    public ComponentTests()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddSingleton<TimeProvider>(_time);
        _context.Services.AddSingleton(Options.Create(new StoryblokOptions { ApiKeyPublic = "public-token" }));
        // The layout renders the Visual Editor bridge, which needs the client; nothing here loads a story.
        _context.Services.AddSingleton(services => new StoryblokStoryClient(
            services.GetRequiredService<IOptions<StoryblokOptions>>(),
            new StoryblokComponentTypes(Array.Empty<System.Reflection.Assembly>()),
            _storyblok,
            new PrerenderedStories(),
            _time,
            NullLogger<StoryblokStoryClient>.Instance));
    }

    public void Dispose()
    {
        _context.Dispose();
        _storyblok.Dispose();
    }

    [Fact]
    public void Header_ShouldDownloadPhotoAtDisplayedSize_WhenProfileHasPhoto()
    {
        // Arrange
        const string photo = "https://a.storyblok.com/f/268473/608x912/b7730a2cf6/profile.jpg";

        // Act
        IRenderedComponent<Header> sut = _context.Render<Header>(parameters => parameters.Add(component => component.Model,
            new StoryblokHeader { Profile = new StoryblokAsset { Url = photo } }));

        // Assert
        // Shown 128 px square in a circle: the part the circle shows, 2x for sharp screens.
        AngleSharp.Dom.IElement image = sut.Find("img");
        image.GetAttribute("src").ShouldBe($"{photo}/m/0x304:608x912/128x128");
        image.GetAttribute("srcset").ShouldBe($"{photo}/m/0x304:608x912/128x128 1x, {photo}/m/0x304:608x912/256x256 2x");
    }

    [Fact]
    public void Header_ShouldCarryItsMarker_WhenRenderedFromTheEditor()
    {
        // Act
        IRenderedComponent<Header> sut = _context.Render<Header>(parameters => parameters.Add(component => component.Model,
            new StoryblokHeader { Editable = """<!--#storyblok#{"uid":"abc","id":"42"}-->""" }));

        // Assert
        sut.Find("header").GetAttribute("data-blok-uid").ShouldBe("42-abc");
    }

    [Fact]
    public void HeaderAndLinkList_ShouldCarryNoEditorMarkers_WhenPublished()
    {
        // Act
        IRenderedComponent<Header> header = _context.Render<Header>(parameters => parameters.Add(component => component.Model,
            new StoryblokHeader()));
        IRenderedComponent<LinkList> list = _context.Render<LinkList>(parameters => parameters.Add(component => component.Model,
            [new StoryblokLink { Label = "GitHub" }]));

        // Assert
        header.Markup.ShouldNotContain("data-blok", Case.Sensitive);
        list.Markup.ShouldNotContain("data-blok", Case.Sensitive);
    }

    [Fact]
    public void TypedText_ShouldShowFirstTagline_WhenTypingHasNotStarted()
    {
        // Act
        IRenderedComponent<TypedText> sut = _context.Render<TypedText>(parameters => parameters.Add(text => text.Model,
            [new StoryblokText { Text = "  " }, new StoryblokText { Text = " Tea enthusiast " }, new StoryblokText { Text = "Developer" }]));

        // Assert
        // The text comes from CSS (attr(data-first)) rather than the element's content, which typed.js owns.
        AngleSharp.Dom.IElement line = sut.Find("p[aria-hidden]");
        line.GetAttribute("data-first").ShouldBe("Tea enthusiast");
        line.TextContent.ShouldBe(string.Empty);
    }

    [Fact]
    public void StoryblokStatus_ShouldSayNothingThenSayLoading_WhenTheFirstSecondHasPassed()
    {
        // Arrange
        IRenderedComponent<StoryblokStatus> sut = RenderStatus(ContentStatus.Loading);

        // Act & Assert
        LiveRegion(sut).ShouldBeEmpty();

        _time.Advance(TimeSpan.FromSeconds(1));
        sut.WaitForAssertion(() => LiveRegion(sut).ShouldBe("Loading links…"));
    }

    [Fact]
    public void StoryblokStatus_ShouldAskForPatience_WhenTheLoadIsSlowAndThenVerySlow()
    {
        // Arrange
        IRenderedComponent<StoryblokStatus> sut = RenderStatus(ContentStatus.Loading);

        // Act
        _time.Advance(TimeSpan.FromSeconds(3));

        // Assert
        sut.WaitForAssertion(() => LiveRegion(sut).ShouldStartWith("Still loading."));

        // Act
        _time.Advance(TimeSpan.FromSeconds(5));

        // Assert
        sut.WaitForAssertion(() => LiveRegion(sut).ShouldStartWith("This is taking longer than usual."));
    }

    [Fact]
    public void StoryblokStatus_ShouldCountFromZeroAgain_WhenANewLoadStarts()
    {
        // Arrange
        IRenderedComponent<StoryblokStatus> sut = RenderStatus(ContentStatus.Loading);
        _time.Advance(TimeSpan.FromSeconds(4));

        sut.WaitForAssertion(() => LiveRegion(sut).ShouldStartWith("Still loading."));

        // Act
        sut.Render(parameters => parameters.Add(component => component.Status, ContentStatus.Done));

        // Assert
        LiveRegion(sut).ShouldBeEmpty();

        // Act
        sut.Render(parameters => parameters.Add(component => component.Status, ContentStatus.Loading));

        // Assert
        LiveRegion(sut).ShouldBeEmpty();
        _time.Advance(TimeSpan.FromSeconds(1));
        sut.WaitForAssertion(() => LiveRegion(sut).ShouldBe("Loading links…"));
    }

    [Fact]
    public void StoryblokStatus_ShouldOfferNoRetry_WhenContentIsUnreadable()
    {
        // Act
        IRenderedComponent<StoryblokStatus> sut = RenderStatus(ContentStatus.Unreadable);

        // Assert
        sut.Markup.ShouldContain("needs a fix first", Case.Sensitive);
        sut.FindAll("button").ShouldBeEmpty();
    }

    [Fact]
    public void MainLayout_ShouldShowErrorMessageAndWayBack_WhenPageFails()
    {
        // Arrange
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("Certificate/broken");

        // Act
        IRenderedComponent<MainLayout> sut = RenderLayout<FailingPage>();

        // Assert
        sut.Markup.ShouldContain("Something went wrong", Case.Sensitive);
        sut.Find("a.action-button").TextContent.ShouldBe("Back to the links");
    }

    [Fact]
    public void MainLayout_ShouldOfferNoLinkBackToItself_WhenHomePageFails()
    {
        // Act
        IRenderedComponent<MainLayout> sut = RenderLayout<FailingPage>();

        // Assert
        sut.Markup.ShouldContain("Something went wrong", Case.Sensitive);
        sut.FindAll("a.action-button").ShouldBeEmpty();
    }

    [Fact]
    public void MainLayout_ShouldClearError_WhenMovingToAnotherPage()
    {
        // Arrange
        IRenderedComponent<MainLayout> sut = RenderLayout<FailingPage>();

        // Act
        sut.Render(parameters => parameters.Add(component => component.Body, Page<WorkingPage>()));

        // Assert
        sut.Markup.ShouldNotContain("Something went wrong", Case.Sensitive);
        sut.Markup.ShouldContain("A working page", Case.Sensitive);
    }

    private IRenderedComponent<StoryblokStatus> RenderStatus(ContentStatus status) =>
        _context.Render<StoryblokStatus>(parameters => parameters
            .Add(component => component.Status, status)
            .Add(component => component.Subject, "links"));

    private IRenderedComponent<MainLayout> RenderLayout<TPage>() where TPage : IComponent =>
        _context.Render<MainLayout>(parameters => parameters.Add(component => component.Body, Page<TPage>()));

    private static RenderFragment Page<TPage>() where TPage : IComponent => builder =>
    {
        builder.OpenComponent<TPage>(0);
        builder.CloseComponent();
    };

    private static string LiveRegion(IRenderedComponent<StoryblokStatus> status) => status.Find("p[role=status]").TextContent;

    private sealed class FailingPage : ComponentBase
    {
        protected override void OnInitialized() => throw new InvalidOperationException("The page broke.");
    }

    private sealed class WorkingPage : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
            builder.AddContent(0, "A working page");
    }
}
