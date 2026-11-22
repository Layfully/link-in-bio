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
    public void Configuration_binds_both_webhook_credentials()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeployTrigger:WebhookSecret"] = "test-secret",
            ["DeployTrigger:GitHubToken"] = "test-token",
        }).Build();

        DeployTriggerOptions settings = new();
        configuration.GetSection(DeployTriggerOptions.Section).Bind(settings);

        Assert.Equal("test-secret", settings.WebhookSecret);
        Assert.Equal("test-token", settings.GitHubToken);
    }

    [Fact]
    public async Task The_right_secret_starts_the_deploy_workflow_on_main()
    {
        DeployTriggerResult result = await Trigger("s3cret");

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        HttpRequestMessage request = Assert.Single(_github.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(LinkInBioDispatch, request.RequestUri!.ToString());
        Assert.Equal("Bearer github-token", request.Headers.Authorization!.ToString());
        Assert.Equal("link-in-bio-deploy-trigger/1.0", request.Headers.UserAgent.ToString());
        Assert.Equal("application/vnd.github+json", request.Headers.Accept.ToString());
        Assert.Equal("2022-11-28", Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
        Assert.Equal(MainDispatchBody, Assert.Single(_github.Bodies));
    }

    [Fact]
    public void This_sites_workflow_exists_and_can_be_started_this_way()
    {
        string workflow = Path.Combine(RepositoryRoot(), ".github", "workflows", DeploySite.LinkInBio.Workflow);

        Assert.True(File.Exists(workflow), $"{workflow} doesn't exist.");
        Assert.Contains("workflow_dispatch:", File.ReadAllText(workflow), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("s3cre")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_wrong_or_missing_secret_is_refused_without_asking_GitHub(string? secret)
    {
        DeployTriggerResult result = await Trigger(secret);

        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task Without_a_webhook_secret_every_call_is_refused()
    {
        DeployTriggerResult result = await Trigger("s3cret", new DeployTriggerOptions { GitHubToken = "github-token" });

        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task Without_a_token_a_stranger_still_learns_only_that_the_secret_is_wrong()
    {
        DeployTriggerOptions settings = new() { WebhookSecret = "s3cret" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await Trigger("wrong", settings)).Status);
        Assert.Equal(HttpStatusCode.InternalServerError, (await Trigger("s3cret", settings)).Status);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task A_refusal_from_GitHub_is_reported_without_its_details_and_not_tried_again()
    {
        _github.Answers.Enqueue(HttpStatusCode.Forbidden);

        DeployTriggerResult result = await Trigger("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Contains("403", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Resource not accessible", result.Message, StringComparison.Ordinal);
        Assert.Single(_github.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, null)]
    [InlineData(HttpStatusCode.TooManyRequests, 5)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 60)]
    public async Task A_rate_limit_or_retry_after_fails_without_retrying(HttpStatusCode status, int? delaySeconds)
    {
        using HttpResponseMessage response = new(status);
        if (delaySeconds.HasValue)
        {
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(delaySeconds.Value));
        }
        _github.Answers.Enqueue(response);

        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Single(_github.Requests);
    }

    [Fact]
    public async Task A_passing_failure_at_GitHub_is_tried_again()
    {
        _github.Answers.Enqueue(HttpStatusCode.BadGateway);
        _github.Answers.Enqueue(new HttpRequestException("Connection reset."));

        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        Assert.Equal(3, _github.Requests.Count);
    }

    [Fact]
    public async Task GitHub_out_of_reach_on_every_attempt_is_reported()
    {
        for (int i = 0; i < 3; i++)
        {
            _github.Answers.Enqueue(new HttpRequestException("No such host is known."));
        }

        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Equal(3, _github.Requests.Count);
    }

    [Fact]
    public async Task Each_timed_out_attempt_is_retried_until_the_limit()
    {
        for (int i = 0; i < 3; i++)
        {
            _github.Answers.Enqueue(Timeout.InfiniteTimeSpan);
        }

        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Equal(3, _github.Requests.Count);
    }

    [Fact]
    public async Task The_callers_cancellation_is_not_reported_as_GitHub_out_of_reach()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        _github.Answers.Enqueue(new TaskCanceledException("The operation was canceled.", null, cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Trigger("s3cret", cancellationToken: cancelled.Token));
    }

    [Theory]
    [InlineData("""{"action":"published","full_slug":"certificates/aws"}""")]
    [InlineData("""{"action":"published","full_slug":"about"}""")]
    [InlineData("""{"action":"published","full_slug":"homepage"}""")]
    [InlineData("""{"action":"published","full_slug":"de/portfolio"}""")]
    public async Task A_publish_of_a_story_no_site_builds_starts_nothing(string body)
    {
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Empty(_github.Requests);
    }

    [Theory]
    [InlineData("""{"action":"unpublished","full_slug":"home"}""")]
    [InlineData("""{"action":"published","full_slug":"/home/"}""")]
    public async Task A_change_to_the_home_story_starts_only_this_sites_deploy(string body)
    {
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        Assert.Equal([LinkInBioDispatch], _github.Addresses);
    }

    [Theory]
    [InlineData("""{"text":"The user published the Story Portfolio (portfolio)","action":"published","space_id":268473,"story_id":1,"full_slug":"portfolio"}""")]
    [InlineData("""{"action":"published","full_slug":"pl/portfolio"}""")]
    [InlineData("""{"action":"published","full_slug":"en/portfolio"}""")]
    [InlineData("""{"action":"published","full_slug":"portfolio/projects/website"}""")]
    public async Task A_change_to_a_portfolio_story_starts_only_the_portfolios_deploy(string body)
    {
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        Assert.Equal([PortfolioDispatch], _github.Addresses);
        Assert.Equal(MainDispatchBody, Assert.Single(_github.Bodies));
    }

    // Storyblok may change what it sends; missing a deploy is worse than an extra one.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"action":"deleted","story_id":1}""")]
    [InlineData("""{"action":"published","full_slug":42}""")]
    [InlineData("""["home"]""")]
    public async Task A_call_that_doesnt_name_a_story_starts_every_sites_deploy(string? body)
    {
        DeployTriggerResult result = await Trigger("s3cret", body: body);

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        Assert.Equal([LinkInBioDispatch, PortfolioDispatch], _github.Addresses.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task One_site_refused_is_reported_and_the_other_still_starts()
    {
        _github.Refused = "Layfully/portfolio";

        DeployTriggerResult result = await Trigger("s3cret", body: null);

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Contains("Deploy of Layfully/link-in-bio started.", result.Message, StringComparison.Ordinal);
        Assert.Contains("Layfully/portfolio (403)", result.Message, StringComparison.Ordinal);
        Assert.Equal(2, _github.Requests.Count);
    }

    private const string LinkInBioDispatch =
        "https://api.github.com/repos/Layfully/link-in-bio/actions/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml/dispatches";

    private const string PortfolioDispatch =
        "https://api.github.com/repos/Layfully/portfolio/actions/workflows/azure-static-web-apps-purple-tree-0510d5403.yml/dispatches";

    [Fact]
    public async Task Another_story_with_a_wrong_secret_is_still_refused()
    {
        DeployTriggerResult result = await Trigger("wrong", body: """{"action":"published","full_slug":"portfolio"}""");

        Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
    }

    private const string HomePublished = """{"action":"published","space_id":268473,"story_id":1,"full_slug":"home"}""";
    private const string MainDispatchBody = """{"ref":"main"}""";

    private async Task<DeployTriggerResult> Trigger(string? secret, DeployTriggerOptions? settings = null, CancellationToken? cancellationToken = null,
        string? body = HomePublished)
    {
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(_time);
        services.AddSingleton(Options.Create(settings ?? new DeployTriggerOptions { WebhookSecret = "s3cret", GitHubToken = "github-token" }));
        services.AddDeployTriggerClient().ConfigurePrimaryHttpMessageHandler(() => _github);
        using ServiceProvider provider = services.BuildServiceProvider();
        DeployTrigger trigger = provider.GetRequiredService<DeployTrigger>();
        return await trigger.TriggerAsync(secret, body, cancellationToken ?? TestContext.Current.CancellationToken);
    }

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
