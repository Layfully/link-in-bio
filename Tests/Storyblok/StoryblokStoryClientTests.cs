using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
    private PrerenderedStories _prerendered = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose() => _storyblok.Dispose();

    [Fact]
    public async Task TryLoad_ShouldEscapeEachPartOfTheSlug_WhenTheSlugHasSpecialCharacters()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.NotFound);
        StoryblokStoryClient sut = CreateClient();

        // Act
        await Load<TestPage>(sut, "/certificates/a b?c#d");

        // Assert
        _storyblok.Requests.Single().AbsolutePath.ShouldBe("/v2/cdn/stories/certificates/a%20b%3Fc%23d");
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/certificates/..")]
    [InlineData("/certificates/.")]
    [InlineData("/certificates//x")]
    public async Task TryLoad_ShouldReturnNotFoundWithoutAskingStoryblok_WhenTheSlugCannotNameAStory(string slug)
    {
        // Arrange
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, slug);

        // Assert
        result.Value.ShouldBeOfType<StoryNotFound>();
        _storyblok.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryLoad_ShouldAskStoryblokOnce_WhenTheSlugDiffersOnlyInSlashes()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient sut = CreateClient();

        // Act
        await Load<TestPage>(sut, "/home");
        await Load<TestPage>(sut, "home/");

        // Assert
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TryLoad_ShouldReadTheStoryIntoTheRegisteredComponentClasses_WhenTheStoryIsFound()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "test_fields", "text": "Hello" },
                { "_uid": "33333333-3333-3333-3333-333333333333", "component": "added_later_in_storyblok" }
            ] }
            """));
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        StoryblokStory<TestPage> story = result.Value.ShouldBeOfType<StoryblokStory<TestPage>>();
        story.Name.ShouldBe("Home");
        StoryblokComponent[] body = story.Content.Body!;
        body.Length.ShouldBe(2);
        body[0].ShouldBeOfType<TestFields>().Text.ShouldBe("Hello");
        // A block the app has no class for is kept with its name instead of failing the story.
        body[1].ShouldBeOfType<StoryblokComponent>().Component.ShouldBe("added_later_in_storyblok");
    }

    [Fact]
    public async Task TryLoad_ShouldReturnNotFound_WhenTheStoryIsMissing()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/nothing");

        // Assert
        result.Value.ShouldBeOfType<StoryNotFound>();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task TryLoad_ShouldReturnUnavailable_WhenStoryblokAnswersWithAnError(HttpStatusCode status)
    {
        // Arrange
        _storyblok.Answer(status, "{}");
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        result.Value.ShouldBeOfType<StoryblokUnavailable>();
    }

    [Fact]
    public async Task TryLoad_ShouldReturnUnavailable_WhenStoryblokIsUnreachable()
    {
        // Arrange
        _storyblok.Fail(new HttpRequestException("No connection."));
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        result.Value.ShouldBeOfType<StoryblokUnavailable>();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{ "cv": 1 }""")] // no story in the answer
    [InlineData("""{ "story": { "content": [] } }""")] // content that isn't a block
    public async Task TryLoad_ShouldReturnUnreadableNotUnavailable_WhenTheSiteCannotReadTheAnswer(string body)
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, body);
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        result.Value.ShouldBeOfType<StoryUnreadable>();
    }

    [Fact]
    public async Task TryLoad_ShouldReturnUnreadable_WhenTheStoryHasNoContentBlock()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, """{ "story": { "name": "Empty", "content": null } }""");
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        result.Value.ShouldBeOfType<StoryUnreadable>();
    }

    [Fact]
    public async Task TryLoad_ShouldLoadEveryTime_WhenTheCacheDurationIsZero()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient sut = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", CacheDurationSeconds = 0 });

        // Act
        StoryLoadResult<TestPage> first = await Load<TestPage>(sut, "/home");
        StoryLoadResult<TestPage> second = await Load<TestPage>(sut, "/home");

        // Assert
        first.Value.ShouldBeOfType<StoryblokStory<TestPage>>();
        second.Value.ShouldBeOfType<StoryblokStory<TestPage>>();
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TryLoad_ShouldReturnUnreadable_WhenTheStoryIsOfAnotherKind()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Fields", """{ "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_fields" }"""));
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        result.Value.ShouldBeOfType<StoryUnreadable>();
    }

    [Fact]
    public async Task TryLoad_ShouldCancelInsteadOfReportingAnOutage_WhenTheCallerLeavesThePage()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient sut = CreateClient();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => Load<TestPage>(sut, "/home", cancellation.Token));
    }

    [Fact]
    public async Task TryLoad_ShouldAskStoryblokOncePerSlug_WhenStoriesArePublishedOrMissing()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        _storyblok.Answer(HttpStatusCode.NotFound, "{}");
        StoryblokStoryClient sut = CreateClient();

        // Act
        await Load<TestPage>(sut, "/home");
        await Load<TestPage>(sut, "/home");
        await Load<TestPage>(sut, "/nothing");
        await Load<TestPage>(sut, "/nothing");

        // Assert
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TryLoad_ShouldLoadAgain_WhenTheCacheTimeIsUp()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Before", Page()));
        _storyblok.Answer(HttpStatusCode.OK, Story("After", Page()));
        StoryblokStoryClient sut = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", CacheDurationSeconds = 60 });

        // Act
        await Load<TestPage>(sut, "/home");
        _time.Advance(TimeSpan.FromSeconds(59));
        await Load<TestPage>(sut, "/home");
        _time.Advance(TimeSpan.FromSeconds(1));
        StoryLoadResult<TestPage> later = await Load<TestPage>(sut, "/home");

        // Assert
        later.Value.ShouldBeOfType<StoryblokStory<TestPage>>().Name.ShouldBe("After");
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TryLoad_ShouldSendThePublicTokenAndTheLastCacheVersion_WhenLoadingPublishedContent()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page(), cacheVersion: 1700000000));
        _storyblok.Answer(HttpStatusCode.OK, Story("Other", Page()));
        StoryblokStoryClient sut = CreateClient();

        // Act
        await Load<TestPage>(sut, "/home");
        await Load<TestPage>(sut, "/other");

        // Assert
        Uri first = _storyblok.Requests[0], second = _storyblok.Requests[1];
        first.AbsolutePath.ShouldBe("/v2/cdn/stories/home");
        first.Query.ShouldContain("version=published", Case.Sensitive);
        first.Query.ShouldContain("token=public-token", Case.Sensitive);
        first.Query.ShouldNotContain("cv=", Case.Sensitive);
        second.Query.ShouldContain("cv=1700000000", Case.Sensitive);
    }

    // Storyblok's CDN answers a cache version it has seen before with that version's content, so a story loaded again
    // with the old one would never show a publish.
    [Fact]
    public async Task TryLoad_ShouldLeaveOutTheCacheVersion_WhenTheCacheTimeIsUp()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Before", Page(), cacheVersion: 1700000000));
        _storyblok.Answer(HttpStatusCode.OK, Story("After", Page(), cacheVersion: 1700000500));
        _storyblok.Answer(HttpStatusCode.OK, Story("Other", Page()));
        StoryblokStoryClient sut = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", CacheDurationSeconds = 60 });

        // Act
        await Load<TestPage>(sut, "/home");
        _time.Advance(TimeSpan.FromSeconds(60));
        await Load<TestPage>(sut, "/home");
        await Load<TestPage>(sut, "/other");

        // Assert
        _storyblok.Requests[1].Query.ShouldNotContain("cv=", Case.Sensitive);
        _storyblok.Requests[2].Query.ShouldContain("cv=1700000500", Case.Sensitive);
    }

    // The CDN answers a request that sends a cache version with that same version. Its time must still count from when it
    // was first seen, or loading another story now and then would keep it in use forever.
    [Fact]
    public async Task TryLoad_ShouldExpireTheCacheVersion_WhenOtherStoriesAreLoadedWithIt()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("A", Page(), cacheVersion: 1700000000));
        _storyblok.Answer(HttpStatusCode.OK, Story("B", Page(), cacheVersion: 1700000000));
        _storyblok.Answer(HttpStatusCode.OK, Story("A again", Page(), cacheVersion: 1700000500));
        StoryblokStoryClient sut = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", CacheDurationSeconds = 60 });

        // Act
        await Load<TestPage>(sut, "/a");
        _time.Advance(TimeSpan.FromSeconds(50));
        await Load<TestPage>(sut, "/b");
        _time.Advance(TimeSpan.FromSeconds(20));
        StoryLoadResult<TestPage> again = await Load<TestPage>(sut, "/a");

        // Assert
        _storyblok.Requests[1].Query.ShouldContain("cv=1700000000", Case.Sensitive);
        _storyblok.Requests[2].Query.ShouldNotContain("cv=", Case.Sensitive);
        again.Value.ShouldBeOfType<StoryblokStory<TestPage>>().Name.ShouldBe("A again");
    }

    [Fact]
    public async Task TryLoad_ShouldLoadDraftsWithThePreviewTokenAndNeverCacheThem_WhenInTheEditor()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient sut = CreateClient(new StoryblokOptions { ApiKeyPublic = "public-token", ApiKeyPreview = "preview-token", IsInEditor = true });

        // Act
        await Load<TestPage>(sut, "/home");
        await Load<TestPage>(sut, "/home");

        // Assert
        _storyblok.Requests.Count.ShouldBe(2);
        _storyblok.Requests[0].Query.ShouldContain("version=draft", Case.Sensitive);
        _storyblok.Requests[0].Query.ShouldContain("token=preview-token", Case.Sensitive);
    }

    [Fact]
    public async Task TryLoad_ShouldReadStoryblokValuesIntoThePropertyTypes_WhenTheFieldsHaveOtherTypes()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Fields", """
            {
              "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_fields",
              "text": 42, "flag_text": true, "option": ["first", "second", { "nested": ["x"] }], "nothing": null,
              "image": { "filename": "https://a.storyblok.com/f/1/600x400/abc/x.jpg", "alt": "A picture" },
              "empty_image": "", "image_without_file": { "filename": "" }
            }
            """));
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestFields> result = await Load<TestFields>(sut, "/fields");

        // Assert
        TestFields fields = result.Value.ShouldBeOfType<StoryblokStory<TestFields>>().Content;
        fields.Text.ShouldBe("42");
        fields.FlagText.ShouldBe("true");
        fields.Option.ShouldBe("first"); // a multi-option field: the first entry, the rest skipped cleanly
        fields.Nothing.ShouldBeNull();
        fields.Image.ShouldNotBeNull().Alt.ShouldBe("A picture");
        fields.EmptyImage.ShouldBeNull();
        fields.ImageWithoutFile.ShouldBeNull();
    }

    [Fact]
    public async Task TryLoad_ShouldUseThePrerenderedStoryWithoutAskingStoryblok_WhenOneExists()
    {
        // Arrange
        _prerendered.AddAll($$"""{ "home": {{Story("Prerendered", Page())}} }""");
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage> result = await Load<TestPage>(sut, "/home");

        // Assert
        StoryblokStory<TestPage> story = result.Value.ShouldBeOfType<StoryblokStory<TestPage>>();
        story.Name.ShouldBe("Prerendered");
        _storyblok.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryRefresh_ShouldReturnNull_WhenTheStoryIsUnchanged()
    {
        // Arrange
        // Recorded and embedded the way the deploy does it, with characters the embedding escapes.
        string response = Story("Me & <you>", Page());
        _storyblok.Answer(HttpStatusCode.OK, response);
        _prerendered.IsRecording = true;
        await Load<TestPage>(CreateClient(), "/home");
        PrerenderedStories embedded = new();
        embedded.AddAll(_prerendered.ToJson());
        _prerendered = embedded;
        _storyblok.Answer(HttpStatusCode.OK, response);
        StoryblokStoryClient sut = CreateClient();
        await Load<TestPage>(sut, "/home");

        // Act
        StoryLoadResult<TestPage>? refreshed = await Refresh<TestPage>(sut, "/home");

        // Assert
        refreshed.ShouldBeNull();
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TryRefresh_ShouldReturnTheNewStory_WhenTheStoryChanged()
    {
        // Arrange
        _prerendered.AddAll($$"""{ "home": {{Story("Before", Page())}} }""");
        _storyblok.Answer(HttpStatusCode.OK, Story("After", Page()));
        StoryblokStoryClient sut = CreateClient();
        await Load<TestPage>(sut, "/home");

        // Act
        StoryLoadResult<TestPage>? refreshed = await Refresh<TestPage>(sut, "/home");

        // Assert
        object? value = refreshed?.Value;
        value.ShouldBeOfType<StoryblokStory<TestPage>>().Name.ShouldBe("After");
    }

    [Fact]
    public async Task TryRefresh_ShouldReturnNullWithoutAsking_WhenTheStoryWasNotPrerendered()
    {
        // Arrange
        StoryblokStoryClient sut = CreateClient();

        // Act
        StoryLoadResult<TestPage>? refreshed = await Refresh<TestPage>(sut, "/home");

        // Assert
        refreshed.ShouldBeNull();
        _storyblok.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TryLoad_ShouldRecordTheResponseOfEachStory_WhenRecording()
    {
        // Arrange
        string response = Story("Home", Page());
        _storyblok.Answer(HttpStatusCode.OK, response);
        _prerendered.IsRecording = true;
        StoryblokStoryClient sut = CreateClient();

        // Act
        await Load<TestPage>(sut, "/home");

        // Assert
        _prerendered.Recorded["home"].ShouldBe(response);
    }

    [Fact]
    public async Task ToJson_ShouldNotEndAScriptBlock_WhenAStoryContainsScriptMarkup()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("</script><!--", Page()));
        PrerenderedStories sut = _prerendered;
        sut.IsRecording = true;
        await Load<TestPage>(CreateClient(), "/home");

        // Act
        string json = sut.ToJson();

        // Assert
        json.ShouldNotContain("</script", Case.Insensitive);
        json.ShouldNotContain("<!--", Case.Sensitive);
        // The embedded copy still reads back as the same story.
        _prerendered = new PrerenderedStories();
        _prerendered.AddAll(json);
        (await Load<TestPage>(CreateClient(), "/home")).Value.ShouldBeOfType<StoryblokStory<TestPage>>().Name.ShouldBe("</script><!--");
    }

    [Fact]
    public void AddAll_ShouldIgnoreTheJson_WhenItIsUnreadable()
    {
        // Arrange
        PrerenderedStories sut = new();

        // Act
        sut.AddAll("not json");

        // Assert
        sut.Contains("/home").ShouldBeFalse();
    }

    [Fact]
    public void ReadEditedStory_ShouldRaiseTheStoryWithItsSlugAndComponentClasses_WhenInTheEditor()
    {
        // Arrange
        StoryblokStoryClient sut = CreateEditorClient();
        List<StoryblokEdit> edits = [];
        sut.StoryEdited += edits.Add;

        // Act
        sut.ReadEditedStory(EditedStory("home", """
            { "_uid": "11111111-1111-1111-1111-111111111111", "component": "test_page", "body": [
                { "_uid": "22222222-2222-2222-2222-222222222222", "component": "test_fields", "text": "Hello" }
            ] }
            """));

        // Assert
        StoryblokEdit edit = edits.ShouldHaveSingleItem();
        edit.TryGet("/home", out StoryblokStory<TestPage>? story).ShouldBeTrue();
        story.ShouldNotBeNull();
        story.Name.ShouldBe("Home");
        story.Content.Body!.ShouldHaveSingleItem().ShouldBeOfType<TestFields>().Text.ShouldBe("Hello");
    }

    [Theory]
    [InlineData("certificates/az-204", "/certificates/AZ-204")]
    [InlineData("home", "home/")]
    public void TryGet_ShouldReturnTrue_WhenTheSlugDiffersInCaseOrSlashes(string fullSlug, string asked)
    {
        // Arrange
        StoryblokEdit sut = Edit(fullSlug, Page());

        // Act
        bool found = sut.TryGet(asked, out StoryblokStory<TestPage>? _);

        // Assert
        found.ShouldBeTrue();
    }

    [Fact]
    public void TryGet_ShouldReturnFalse_WhenTheSlugBelongsToAnotherStory()
    {
        // Arrange
        StoryblokEdit sut = Edit("certificates/x", Page());

        // Act
        bool found = sut.TryGet("/home", out StoryblokStory<TestPage>? _);

        // Assert
        found.ShouldBeFalse();
    }

    [Fact]
    public void TryGet_ShouldReturnFalse_WhenTheStoryIsOfAnotherKind()
    {
        // Arrange
        string fields = """{ "_uid": "22222222-2222-2222-2222-222222222222", "component": "test_fields" }""";
        StoryblokEdit sut = Edit("home", fields);

        // Act
        bool found = sut.TryGet("/home", out StoryblokStory<TestPage>? _);

        // Assert
        found.ShouldBeFalse();
    }

    [Fact]
    public void ReadEditedStory_ShouldRaiseNothing_WhenOutsideTheEditor()
    {
        // Arrange
        StoryblokStoryClient sut = CreateClient();
        List<StoryblokEdit> edits = [];
        sut.StoryEdited += edits.Add;

        // Act
        sut.ReadEditedStory(EditedStory("home", Page()));

        // Assert
        edits.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void ReadEditedStory_ShouldRaiseNothing_WhenTheStoryCannotBeRead(string json)
    {
        // Arrange
        StoryblokStoryClient sut = CreateEditorClient();
        List<StoryblokEdit> edits = [];
        sut.StoryEdited += edits.Add;

        // Act
        sut.ReadEditedStory(json);

        // Assert
        edits.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadEditedStory_ShouldNotPutTheStoryInTheCache_WhenInTheEditor()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.OK, Story("Home", Page()));
        StoryblokStoryClient sut = CreateEditorClient(cacheDurationSeconds: 60);

        // Act
        sut.ReadEditedStory(EditedStory("home", Page()));
        await Load<TestPage>(sut, "/home");

        // Assert
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    private StoryblokEdit Edit(string fullSlug, string content)
    {
        StoryblokStoryClient client = CreateEditorClient();
        StoryblokEdit? edit = null;
        client.StoryEdited += raised => edit = raised;
        client.ReadEditedStory(EditedStory(fullSlug, content));
        return edit.ShouldBeOfType<StoryblokEdit>();
    }

    private StoryblokStoryClient CreateEditorClient(int cacheDurationSeconds = 0) => CreateClient(new StoryblokOptions
    {
        ApiKeyPublic = "public-token",
        ApiKeyPreview = "preview-token",
        IsInEditor = true,
        CacheDurationSeconds = cacheDurationSeconds,
    });

    // The bridge sends the story itself, without the { "story": ... } around it that the API answers with.
    private static string EditedStory(string fullSlug, string content) =>
        $$"""{ "name": "Home", "full_slug": "{{fullSlug}}", "content": {{content}} }""";

    private static Task<StoryLoadResult<T>?> Refresh<T>(StoryblokStoryClient client, string slug) where T : StoryblokComponent =>
        client.Story().WithSlug(slug).TryRefresh<T>(TestContext.Current.CancellationToken);

    private static Task<StoryLoadResult<T>> Load<T>(StoryblokStoryClient client, string slug, CancellationToken? cancellationToken = null)
        where T : StoryblokComponent =>
        client.Story().WithSlug(slug).TryLoad<T>(cancellationToken ?? TestContext.Current.CancellationToken);

    private StoryblokStoryClient CreateClient(StoryblokOptions? options = null) => new(
        Options.Create(options ?? new StoryblokOptions { ApiKeyPublic = "public-token" }),
        new StoryblokComponentTypes([typeof(TestPage), typeof(TestFields)]),
        _storyblok,
        _prerendered,
        _time,
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
    [JsonPropertyName("image")] public StoryblokAsset? Image { get; set; }
    [JsonPropertyName("empty_image")] public StoryblokAsset? EmptyImage { get; set; }
    [JsonPropertyName("image_without_file")] public StoryblokAsset? ImageWithoutFile { get; set; }
}
