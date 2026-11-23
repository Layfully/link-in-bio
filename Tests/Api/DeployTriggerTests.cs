using Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;

namespace LinkInBio.Tests.Api;

/// <summary>What the Storyblok webhook does with a call: start the deploy, or refuse and say why.</summary>
public sealed class DeployTriggerTests : IDisposable
{
    private readonly FakeGitHub _github = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose() => _github.Dispose();

    [Fact]
    public void Bind_ShouldFillBothWebhookCredentials_WhenConfigurationHasThem()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeployTrigger:WebhookSecret"] = "test-secret",
            ["DeployTrigger:GitHubToken"] = "test-token",
        }).Build();

        DeployTriggerOptions sut = new();

        // Act
        configuration.GetSection(DeployTriggerOptions.Section).Bind(sut);

        // Assert
        sut.WebhookSecret.ShouldBe("test-secret");
        sut.GitHubToken.ShouldBe("test-token");
    }

    [Fact]
    public async Task TriggerAsync_ShouldStartTheDeployWorkflowOnMain_WhenTheSecretIsRight()
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Accepted);
        HttpRequestMessage request = _github.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.ToString().ShouldBe(LinkInBioDispatch);
        request.Headers.Authorization!.ToString().ShouldBe("Bearer github-token");
        request.Headers.UserAgent.ToString().ShouldBe("link-in-bio-deploy-trigger/1.0");
        request.Headers.Accept.ToString().ShouldBe("application/vnd.github+json");
        request.Headers.GetValues("X-GitHub-Api-Version").ShouldHaveSingleItem().ShouldBe("2022-11-28");
        _github.Bodies.ShouldHaveSingleItem().ShouldBe(MainDispatchBody);
    }

    [Fact]
    public void Workflow_ShouldExistAndAcceptDispatches_WhenConfiguredForThisSite()
    {
        // Arrange
        DeploySite site = ConfiguredSites().Where(site => site.Repository == "Layfully/link-in-bio").ShouldHaveSingleItem();
        string workflow = Path.Combine(RepositoryRoot(), ".github", "workflows", site.Workflow);

        // Act & Assert
        File.Exists(workflow).ShouldBeTrue($"{workflow} doesn't exist.");
        File.ReadAllText(workflow).ShouldContain("workflow_dispatch:", Case.Sensitive);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("s3cre")]
    [InlineData("")]
    [InlineData(null)]
    public async Task TriggerAsync_ShouldRefuseWithoutAskingGitHub_WhenTheSecretIsWrongOrMissing(string? secret)
    {
        // Act
        DeployTriggerResult result = await Trigger(secret);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Unauthorized);
        _github.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TriggerAsync_ShouldRefuseEveryCall_WhenThereIsNoWebhookSecret()
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret", new DeployTriggerOptions { GitHubToken = "github-token" });

        // Assert
        result.Status.ShouldBe(HttpStatusCode.InternalServerError);
        _github.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TriggerAsync_ShouldOnlyTellAStrangerTheSecretIsWrong_WhenTheTokenIsMissing()
    {
        // Arrange
        DeployTriggerOptions settings = new() { WebhookSecret = "s3cret" };

        // Act
        DeployTriggerResult stranger = await Trigger("wrong", settings);
        DeployTriggerResult owner = await Trigger("s3cret", settings);

        // Assert
        stranger.Status.ShouldBe(HttpStatusCode.Unauthorized);
        owner.Status.ShouldBe(HttpStatusCode.InternalServerError);
        _github.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TriggerAsync_ShouldReportTheRefusalWithoutDetailsAndNotRetry_WhenGitHubRefuses()
    {
        // Arrange
        _github.Answers.Enqueue(HttpStatusCode.Forbidden);

        // Act
        DeployTriggerResult result = await Trigger("s3cret");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.BadGateway);
        result.Message.ShouldContain("403", Case.Sensitive);
        result.Message.ShouldNotContain("Resource not accessible", Case.Sensitive);
        _github.Requests.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, null)]
    [InlineData(HttpStatusCode.TooManyRequests, 5)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 60)]
    public async Task TriggerAsync_ShouldFailWithoutRetrying_WhenGitHubRateLimitsOrAsksToRetryLater(HttpStatusCode status, int? delaySeconds)
    {
        // Arrange
        using HttpResponseMessage response = new(status);
        if (delaySeconds.HasValue)
        {
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(delaySeconds.Value));
        }
        _github.Answers.Enqueue(response);

        // Act
        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.BadGateway);
        _github.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TriggerAsync_ShouldTryAgain_WhenGitHubFailsForAMoment()
    {
        // Arrange
        _github.Answers.Enqueue(HttpStatusCode.BadGateway);
        _github.Answers.Enqueue(new HttpRequestException("Connection reset."));

        // Act
        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Accepted);
        _github.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task TriggerAsync_ShouldReportBadGateway_WhenGitHubIsOutOfReachOnEveryAttempt()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            _github.Answers.Enqueue(new HttpRequestException("No such host is known."));
        }

        // Act
        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.BadGateway);
        _github.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task TriggerAsync_ShouldRetryEachAttemptUntilTheLimit_WhenEveryAttemptTimesOut()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            _github.Answers.Enqueue(Timeout.InfiniteTimeSpan);
        }

        // Act
        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.BadGateway);
        _github.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task TriggerAsync_ShouldNotReportGitHubOutOfReach_WhenTheCallerCancels()
    {
        // Arrange
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        _github.Answers.Enqueue(new TaskCanceledException("The operation was canceled.", null, cancelled.Token));

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => Trigger("s3cret", cancellationToken: cancelled.Token));
    }

    [Theory]
    [InlineData("""{"action":"published","full_slug":"certificates/aws"}""")]
    [InlineData("""{"action":"published","full_slug":"about"}""")]
    [InlineData("""{"action":"published","full_slug":"homepage"}""")]
    [InlineData("""{"action":"published","full_slug":"de/portfolio"}""")]
    public async Task TriggerAsync_ShouldStartNothing_WhenNoSiteBuildsThePublishedStory(string body)
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.OK);
        _github.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{"action":"unpublished","full_slug":"home"}""")]
    [InlineData("""{"action":"published","full_slug":"/home/"}""")]
    public async Task TriggerAsync_ShouldStartOnlyThisSitesDeploy_WhenTheHomeStoryChanges(string body)
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Accepted);
        _github.Addresses.ShouldBe(new[] { LinkInBioDispatch });
    }

    [Theory]
    [InlineData("""{"text":"The user published the Story Portfolio (portfolio)","action":"published","space_id":268473,"story_id":1,"full_slug":"portfolio"}""")]
    [InlineData("""{"action":"published","full_slug":"pl/portfolio"}""")]
    [InlineData("""{"action":"published","full_slug":"en/portfolio"}""")]
    [InlineData("""{"action":"published","full_slug":"portfolio/projects/website"}""")]
    public async Task TriggerAsync_ShouldStartOnlyThePortfoliosDeploy_WhenAPortfolioStoryChanges(string body)
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Accepted);
        _github.Addresses.ShouldBe(new[] { PortfolioDispatch });
        _github.Bodies.ShouldHaveSingleItem().ShouldBe(MainDispatchBody);
    }

    // Storyblok may change what it sends; missing a deploy is worse than an extra one.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"action":"deleted","story_id":1}""")]
    [InlineData("""{"action":"published","full_slug":42}""")]
    [InlineData("""["home"]""")]
    public async Task TriggerAsync_ShouldStartEverySitesDeploy_WhenTheCallDoesntNameAStory(string? body)
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Accepted);
        _github.Addresses.Order(StringComparer.Ordinal).ShouldBe(new[] { LinkInBioDispatch, PortfolioDispatch });
    }

    [Fact]
    public async Task TriggerAsync_ShouldReportBadGatewayAndStartTheOtherSite_WhenOneSiteIsRefused()
    {
        // Arrange
        _github.Refused = "Layfully/portfolio";

        // Act
        DeployTriggerResult result = await Trigger("s3cret", body: null);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.BadGateway);
        result.Message.ShouldContain("Deploy of Layfully/link-in-bio started.", Case.Sensitive);
        result.Message.ShouldContain("Layfully/portfolio (403)", Case.Sensitive);
        _github.Requests.Count.ShouldBe(2);
    }

    private const string LinkInBioDispatch =
        "https://api.github.com/repos/Layfully/link-in-bio/actions/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml/dispatches";

    private const string PortfolioDispatch =
        "https://api.github.com/repos/Layfully/portfolio/actions/workflows/azure-static-web-apps-purple-tree-0510d5403.yml/dispatches";

    [Fact]
    public async Task TriggerAsync_ShouldRefuse_WhenTheSecretIsWrongForAnotherStory()
    {
        // Act
        DeployTriggerResult result = await Trigger("wrong", body: """{"action":"published","full_slug":"portfolio"}""");

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private const string HomePublished = """{"action":"published","space_id":268473,"story_id":1,"full_slug":"home"}""";
    private const string MainDispatchBody = """{"ref":"main"}""";

    [Fact]
    public async Task TriggerAsync_ShouldRouteToTheConfiguredWorkflowAndBranch_WhenAForkConfiguresItsOwnSite()
    {
        // Arrange
        DeploySite site = new() { Repository = "example/links", Workflow = "publish.yml", Ref = "production", Slugs = ["profile"] };

        // Act
        DeployTriggerResult result = await Trigger("s3cret", body: """{"full_slug":"profile"}""", sites: [site]);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.Accepted);
        _github.Addresses.ShouldHaveSingleItem().ShouldBe("https://api.github.com/repos/example/links/actions/workflows/publish.yml/dispatches");
        _github.Bodies.ShouldHaveSingleItem().ShouldBe("""{"ref":"production"}""");
    }

    [Fact]
    public async Task TriggerAsync_ShouldStartNoWorkflow_WhenTheTargetListIsEmpty()
    {
        // Act
        DeployTriggerResult result = await Trigger("s3cret", sites: []);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.OK);
        _github.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TriggerAsync_ShouldSendNoRequest_WhenTheTargetConfigurationIsInvalid()
    {
        // Arrange
        DeploySite site = new() { Repository = "example", Workflow = "publish.yml", Ref = "main" };

        // Act
        DeployTriggerResult result = await Trigger("s3cret", sites: [site]);

        // Assert
        result.Status.ShouldBe(HttpStatusCode.InternalServerError);
        _github.Requests.ShouldBeEmpty();
    }

    private async Task<DeployTriggerResult> Trigger(string? secret, DeployTriggerOptions? settings = null, CancellationToken? cancellationToken = null,
        string? body = HomePublished, DeploySite[]? sites = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(_time);
        settings ??= new DeployTriggerOptions { WebhookSecret = "s3cret", GitHubToken = "github-token" };
        services.AddSingleton(Options.Create(new DeployTriggerOptions
        {
            WebhookSecret = settings.WebhookSecret,
            GitHubToken = settings.GitHubToken,
            Sites = sites ?? ConfiguredSites(),
        }));
        services.AddDeployTriggerClient().ConfigurePrimaryHttpMessageHandler(() => _github);
        using ServiceProvider provider = services.BuildServiceProvider();
        DeployTrigger sut = provider.GetRequiredService<DeployTrigger>();
        return await sut.TriggerAsync(secret, body, cancellationToken ?? TestContext.Current.CancellationToken);
    }

    private static DeploySite[] ConfiguredSites() => new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(RepositoryRoot(), "Api", "appsettings.json"))
        .Build()
        .GetSection(DeployTriggerOptions.Section)
        .Get<DeployTriggerOptions>()!.Sites;

    // Moves the fake clock on until the trigger has answered, so the pauses between attempts take no real time.
    private async Task<DeployTriggerResult> TriggerWhileTimePasses(string? secret)
    {
        Task<DeployTriggerResult> trigger = Trigger(secret);
        while (!trigger.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        return await trigger;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "link-in-bio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root wasn't found above the test output.");
    }

    // Answers with the queued answers in turn (a status or an exception), then with success; refuses every request for
    // the repository in Refused; keeps what was sent. The sites' requests can arrive at the same time.
    private sealed class FakeGitHub : HttpMessageHandler
    {
        private readonly Lock _lock = new();

        public Queue<object> Answers { get; } = [];

        public string? Refused { get; set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public IEnumerable<string> Addresses => Requests.Select(request => request.RequestUri!.ToString());

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            object? answer;
            lock (_lock)
            {
                Requests.Add(request);
                Bodies.Add(body);
                answer = Refused is not null && request.RequestUri!.AbsolutePath.Contains($"/{Refused}/", StringComparison.Ordinal)
                    ? HttpStatusCode.Forbidden
                    : Answers.TryDequeue(out object? queued) ? queued : HttpStatusCode.OK;
            }

            if (answer is TimeSpan delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return answer switch
            {
                HttpResponseMessage response => response,
                Exception exception => throw exception,
                HttpStatusCode status => new HttpResponseMessage(status)
                {
                    Content = new StringContent("""{"message":"Resource not accessible by personal access token"}""", Encoding.UTF8),
                },
                _ => throw new InvalidOperationException("Answers holds only statuses and exceptions."),
            };
        }
    }
}
