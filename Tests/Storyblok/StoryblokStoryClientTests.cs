using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Storyblok;
using Storyblok.Attributes;
using Storyblok.Client;
using System.Net;
using System.Text.Json.Serialization;

namespace LinkInBio.Tests.Storyblok;

/// <summary>
/// The client against a fake Storyblok: how each answer becomes a result, how the converters read Storyblok's values,
/// and what is cached. The client is built as the app builds it, minus the retry handler.
/// </summary>
public sealed class StoryblokStoryClientTests : IDisposable
{
    private readonly FakeStoryblok _storyblok = new();

    public void Dispose() => _storyblok.Dispose();

    [Fact]
    public async Task Each_part_of_a_slug_is_escaped()
    {
        _storyblok.Answer(HttpStatusCode.NotFound);

        await Load<TestPage>("/certificates/a b?c#d");

        Assert.Equal("/v2/cdn/stories/certificates/a%20b%3Fc%23d", _storyblok.Requests.Single().AbsolutePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/certificates/..")]
    [InlineData("/certificates/.")]
    [InlineData("/certificates//x")]
    public async Task A_slug_that_cant_name_a_story_is_not_found_without_asking_Storyblok(string slug)
    {
        Assert.IsType<StoryNotFound>((await Load<TestPage>(slug)).Value);
        Assert.Empty(_storyblok.Requests);
    }

    [Fact]
    public async Task A_slug_with_or_without_slashes_around_it_is_one_cache_entry()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient client = CreateClient();

        await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);
        await Load<TestPage>(client, "home/", TestContext.Current.CancellationToken);

        Assert.Single(_storyblok.Requests);
    }

    [Fact]
    public async Task A_story_is_read_into_the_registered_component_classes()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "test_fields", "text": "Hello" },
                { "_uid": "33333333-3333-3333-3333-333333333333", "component": "added_later_in_storyblok" }
            ] }
            """));

        StoryblokStory<TestPage> story = Assert.IsType<StoryblokStory<TestPage>>((await Load<TestPage>("/home")).Value);

        Assert.Equal("Home", story.Name);
        Assert.Collection(story.Content.Body!,
            block => Assert.Equal("Hello", Assert.IsType<TestFields>(block).Text),
            // A block the app has no class for is kept with its name instead of failing the story.
            block => Assert.Equal("added_later_in_storyblok", Assert.IsType<StoryblokComponent>(block).Component));
    }

    [Fact]
    public async Task A_missing_story_is_not_found()
    {
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");

        Assert.IsType<StoryNotFound>((await Load<TestPage>("/nothing")).Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task An_error_answer_means_Storyblok_is_unavailable(HttpStatusCode status)
    {
        _storyblok.Answer(status, "{}");

        Assert.IsType<StoryblokUnavailable>((await Load<TestPage>("/home")).Value);
    }

    [Fact]
    public async Task An_unreachable_Storyblok_is_unavailable()
    {
        _storyblok.Fail(new HttpRequestException("No connection."));

        Assert.IsType<StoryblokUnavailable>((await Load<TestPage>("/home")).Value);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{ "cv": 1 }""")] // no story in the answer
    [InlineData("""{ "story": { "content": [] } }""")] // content that isn't a block
    public async Task An_answer_the_site_cant_read_is_unreadable_not_an_outage(string body)
    {
        _storyblok.Answer(HttpStatusCode.OK, body);

        Assert.IsType<StoryUnreadable>((await Load<TestPage>("/home")).Value);
    }

    [Fact]
    public async Task A_story_without_a_content_block_is_unreadable()
    {
        _storyblok.Answer(HttpStatusCode.OK, """{ "story": { "name": "Empty", "content": null } }""");

        Assert.IsType<StoryUnreadable>((await Load<TestPage>("/home")).Value);
    }

    [Fact]
    public async Task A_cache_duration_of_zero_loads_every_time()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient client = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", CacheDurationSeconds = 0 });

        StoryLoadResult<TestPage> first = await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);
        StoryLoadResult<TestPage> second = await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);

        Assert.IsType<StoryblokStory<TestPage>>(first.Value);
        Assert.IsType<StoryblokStory<TestPage>>(second.Value);
        Assert.Equal(2, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task A_story_of_another_kind_is_unreadable()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Fields", """{ "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_fields" }"""));

        Assert.IsType<StoryUnreadable>((await Load<TestPage>("/home")).Value);
    }

    [Fact]
    public async Task Leaving_the_page_cancels_the_load_instead_of_reporting_an_outage()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Load<TestPage>(CreateClient(), "/home", cancellation.Token));
    }

    [Fact]
    public async Task Published_stories_and_missing_stories_are_cached()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");
        StoryblokStoryClient client = CreateClient();

        await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);
        await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);
        await Load<TestPage>(client, "/nothing", TestContext.Current.CancellationToken);
        await Load<TestPage>(client, "/nothing", TestContext.Current.CancellationToken);

        Assert.Equal(2, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task Requests_carry_the_public_token_the_published_version_and_the_last_cache_version()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page(), cacheVersion: 1700000000));
        _storyblok.Answer(HttpStatusCode.OK, Story("Other", Page()));
        StoryblokStoryClient client = CreateClient();

        await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);
        await Load<TestPage>(client, "/other", TestContext.Current.CancellationToken);

        Uri first = _storyblok.Requests[0], second = _storyblok.Requests[1];
        Assert.Equal("/v2/cdn/stories/home", first.AbsolutePath);
        Assert.Contains("version=published", first.Query);
        Assert.Contains("token=public-token", first.Query);
        Assert.DoesNotContain("cv=", first.Query);
        Assert.Contains("cv=1700000000", second.Query);
    }

    [Fact]
    public async Task The_editor_loads_drafts_with_the_preview_token_and_never_caches_them()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient client = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", ApiKeyPreview = "preview-token", IsInEditor = true });

        await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);
        await Load<TestPage>(client, "/home", TestContext.Current.CancellationToken);

        Assert.Equal(2, _storyblok.Requests.Count);
        Assert.Contains("version=draft", _storyblok.Requests[0].Query);
        Assert.Contains("token=preview-token", _storyblok.Requests[0].Query);
    }

    [Fact]
    public async Task Storyblok_values_are_read_into_the_property_types()
    {
        _storyblok.Answer(HttpStatusCode.OK, Story("Fields", """
            {
              "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_fields",
              "text": 42, "flag_text": true, "option": ["first", "second", { "nested": ["x"] }], "nothing": null,
              "number": "7", "number_from_json": 8, "optional_number": "", "broken_number": { "a": 1 },
              "date": "2024-05-01 10:00", "timestamp": "2024-05-01T10:00:00.000Z", "no_date": "",
              "image": { "filename": "https://a.storyblok.com/f/1/600x400/abc/x.jpg", "alt": "A picture" },
              "empty_image": "", "image_without_file": { "filename": "" }
            }
            """));

        TestFields fields = Assert.IsType<StoryblokStory<TestFields>>((await Load<TestFields>("/fields")).Value).Content;

        Assert.Equal("42", fields.Text);
        Assert.Equal("true", fields.FlagText);
        Assert.Equal("first", fields.Option); // a multi-option field: the first entry, the rest skipped cleanly
        Assert.Null(fields.Nothing);
        Assert.Equal(7, fields.Number);
        Assert.Equal(8, fields.NumberFromJson);
        Assert.Null(fields.OptionalNumber);
        Assert.Equal(0, fields.BrokenNumber);
        Assert.Equal(new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc), fields.Date);
        Assert.Equal(DateTimeKind.Utc, fields.Date!.Value.Kind);
        Assert.Equal(new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc), fields.Timestamp);
        Assert.Null(fields.NoDate);
        Assert.Equal("A picture", fields.Image?.Alt);
        Assert.Null(fields.EmptyImage);
        Assert.Null(fields.ImageWithoutFile);
    }

    private Task<StoryLoadResult<T>> Load<T>(string slug) where T : StoryblokComponent =>
        Load<T>(CreateClient(), slug, TestContext.Current.CancellationToken);

    private static Task<StoryLoadResult<T>> Load<T>(StoryblokStoryClient client, string slug, CancellationToken cancellationToken)
        where T : StoryblokComponent =>
        client.Story().WithSlug(slug).TryLoad<T>(cancellationToken);

    private StoryblokStoryClient CreateClient(StoryblokOptions? options = null) => new(
        Options.Create(options ?? new StoryblokOptions { ApiKeyPublic = "public-token" }),
        new StoryblokComponentTypes([typeof(TestPage), typeof(TestFields)]),
        _storyblok,
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<StoryblokStoryClient>.Instance);

    private static string Page() => """{ "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_page", "body": [] }""";

    private static string Story(string name, string content, long cacheVersion = 0) =>
        $$"""{ "story": { "name": "{{name}}", "content": {{content}} }, "cv": {{cacheVersion}} }""";
}

[StoryblokComponent("test_page")]
public sealed class TestPage : StoryblokComponent
{
    [JsonPropertyName("body")]
    public StoryblokComponent[]? Body { get; set; }
}

[StoryblokComponent("test_fields")]
public sealed class TestFields : StoryblokComponent
{
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("flag_text")] public string? FlagText { get; set; }
    [JsonPropertyName("option")] public string? Option { get; set; }
    [JsonPropertyName("nothing")] public string? Nothing { get; set; }
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("number_from_json")] public int NumberFromJson { get; set; }
    [JsonPropertyName("optional_number")] public int? OptionalNumber { get; set; }
    [JsonPropertyName("broken_number")] public int BrokenNumber { get; set; } = -1;
    [JsonPropertyName("date")] public DateTime? Date { get; set; }
    [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; }
    [JsonPropertyName("no_date")] public DateTime? NoDate { get; set; }
    [JsonPropertyName("image")] public StoryblokAsset? Image { get; set; }
    [JsonPropertyName("empty_image")] public StoryblokAsset? EmptyImage { get; set; }
    [JsonPropertyName("image_without_file")] public StoryblokAsset? ImageWithoutFile { get; set; }
}
