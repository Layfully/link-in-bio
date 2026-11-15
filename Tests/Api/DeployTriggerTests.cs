using Api;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task The_right_secret_starts_the_deploy_workflow_on_main()
    {
        DeployTriggerResult result = await Trigger("s3cret");

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        HttpRequestMessage request = Assert.Single(_github.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.github.com/repos/Layfully/link-in-bio/actions/workflows/azure-static-web-apps-gray-tree-0c2e88d03.yml/dispatches",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer github-token", request.Headers.Authorization!.ToString());
        Assert.NotEmpty(request.Headers.UserAgent); // GitHub refuses requests without one
        Assert.Equal("""{"ref":"main"}""", _github.Bodies.Single());
    }

    [Fact]
    public void The_workflow_named_in_the_settings_exists_and_can_be_started_this_way()
    {
        string workflow = Path.Combine(RepositoryRoot(), ".github", "workflows", new DeployTriggerOptions().Workflow);

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
        for (int i = 0; i < DeployTrigger.MaxAttempts; i++)
        {
            _github.Answers.Enqueue(new HttpRequestException("No such host is known."));
        }

        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Equal(DeployTrigger.MaxAttempts, _github.Requests.Count);
    }

    [Fact]
    public async Task HttpClients_timeout_counts_as_out_of_reach()
    {
        for (int i = 0; i < DeployTrigger.MaxAttempts; i++)
        {
            // What HttpClient throws when its Timeout passes: a cancellation the caller didn't ask for.
            _github.Answers.Enqueue(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));
        }

        DeployTriggerResult result = await TriggerWhileTimePasses("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
    }

    [Fact]
    public async Task The_callers_cancellation_is_not_reported_as_GitHub_out_of_reach()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        _github.Answers.Enqueue(new TaskCanceledException("The operation was canceled.", null, cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Trigger("s3cret", cancellationToken: cancelled.Token));
    }

    private Task<DeployTriggerResult> Trigger(string? secret, DeployTriggerOptions? settings = null, CancellationToken? cancellationToken = null)
    {
        HttpClient http = new(_github, disposeHandler: false);
        DeployTrigger trigger = new(http, Options.Create(settings ?? new DeployTriggerOptions { WebhookSecret = "s3cret", GitHubToken = "github-token" }),
            _time, NullLogger<DeployTrigger>.Instance);
        return trigger.TriggerAsync(secret, cancellationToken ?? TestContext.Current.CancellationToken);
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

    // Answers with the queued answers in turn (a status or an exception), then with success; keeps what was sent.
    private sealed class FakeGitHub : HttpMessageHandler
    {
        public Queue<object> Answers { get; } = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

            return (Answers.TryDequeue(out object? answer) ? answer : HttpStatusCode.OK) switch
            {
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
