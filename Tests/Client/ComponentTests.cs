using Bunit;
using Client.Components;
using Client.Data.Components;
using Client.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Storyblok;

namespace LinkInBio.Tests.Client;

/// <summary>The loading hints while content loads, and the layout's message for a page that fails.</summary>
public sealed class ComponentTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly FakeTimeProvider _time = new();

    public ComponentTests()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddSingleton<TimeProvider>(_time);
        _context.Services.AddSingleton(Options.Create(new StoryblokOptions { ApiKeyPublic = "public-token" }));
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void The_typed_line_shows_the_first_tagline_until_typing_starts()
    {
        IRenderedComponent<TypedText> typed = _context.Render<TypedText>(parameters => parameters.Add(text => text.Model,
            [new StoryblokText { Text = "  " }, new StoryblokText { Text = " Tea enthusiast " }, new StoryblokText { Text = "Developer" }]));

        // The text comes from CSS (attr(data-first)) rather than the element's content, which typed.js owns.
        AngleSharp.Dom.IElement line = typed.Find("p[aria-hidden]");
        Assert.Equal("Tea enthusiast", line.GetAttribute("data-first"));
        Assert.Equal(string.Empty, line.TextContent);
    }

    [Fact]
    public void A_load_says_nothing_in_its_first_second_then_says_it_is_loading()
    {
        IRenderedComponent<StoryblokStatus> status = RenderStatus(ContentStatus.Loading);

        Assert.Empty(LiveRegion(status));

        _time.Advance(TimeSpan.FromSeconds(1));
        status.WaitForAssertion(() => Assert.Equal("Loading links…", LiveRegion(status)));
    }

    [Fact]
    public void A_slow_load_says_so_and_a_very_slow_one_asks_for_patience()
    {
        IRenderedComponent<StoryblokStatus> status = RenderStatus(ContentStatus.Loading);

        for (int second = 1; second <= 3; second++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        status.WaitForAssertion(() => Assert.StartsWith("Still loading.", LiveRegion(status)));

        for (int second = 4; second <= 8; second++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        status.WaitForAssertion(() => Assert.StartsWith("This is taking longer than usual.", LiveRegion(status)));
    }

    [Fact]
    public void A_new_load_counts_from_zero_again()
    {
        IRenderedComponent<StoryblokStatus> status = RenderStatus(ContentStatus.Loading);
        for (int second = 1; second <= 4; second++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
        }

        status.WaitForAssertion(() => Assert.StartsWith("Still loading.", LiveRegion(status)));

        status.Render(parameters => parameters.Add(component => component.Status, ContentStatus.Done));
        Assert.Empty(LiveRegion(status));

        status.Render(parameters => parameters.Add(component => component.Status, ContentStatus.Loading));
        Assert.Empty(LiveRegion(status));
        _time.Advance(TimeSpan.FromSeconds(1));
        status.WaitForAssertion(() => Assert.Equal("Loading links…", LiveRegion(status)));
    }

    [Fact]
    public void Content_the_site_cant_read_offers_no_retry()
    {
        IRenderedComponent<StoryblokStatus> status = RenderStatus(ContentStatus.Unreadable);

        Assert.Contains("needs a fix first", status.Markup);
        Assert.Empty(status.FindAll("button"));
    }

    [Fact]
    public void A_failing_page_shows_the_error_message_and_a_way_back()
    {
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("Certificate/broken");

        IRenderedComponent<MainLayout> layout = RenderLayout<FailingPage>();

        Assert.Contains("Something went wrong", layout.Markup);
        Assert.Equal("Back to the links", layout.Find("a.action-button").TextContent);
    }

    [Fact]
    public void A_failing_home_page_offers_no_link_back_to_itself()
    {
        IRenderedComponent<MainLayout> layout = RenderLayout<FailingPage>();

        Assert.Contains("Something went wrong", layout.Markup);
        Assert.Empty(layout.FindAll("a.action-button"));
    }

    [Fact]
    public void Moving_to_another_page_clears_the_error()
    {
        IRenderedComponent<MainLayout> layout = RenderLayout<FailingPage>();

        layout.Render(parameters => parameters.Add(component => component.Body, Page<WorkingPage>()));

        Assert.DoesNotContain("Something went wrong", layout.Markup);
        Assert.Contains("A working page", layout.Markup);
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
