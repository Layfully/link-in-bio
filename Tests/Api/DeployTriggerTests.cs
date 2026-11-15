using Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace LinkInBio.Tests.Api;

/// <summary>What the Storyblok webhook does with a call: start the deploy, or refuse and say why.</summary>
public sealed class DeployTriggerTests : IDisposable
{
    private readonly FakeGitHub _github = new();

    public void Dispose() => _github.Dispose();

    [Fact]
    public async Task The_right_secret_starts_the_deploy()
    {
        DeployTriggerResult result = await Trigger("s3cret");

        Assert.Equal(HttpStatusCode.Accepted, result.Status);
        HttpRequestMessage request = Assert.Single(_github.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.github.com/repos/Layfully/link-in-bio/dispatches", request.RequestUri!.ToString());
        Assert.Equal("Bearer github-token", request.Headers.Authorization!.ToString());
        Assert.NotEmpty(request.Headers.UserAgent); // GitHub refuses requests without one
        Assert.Equal("""{"event_type":"storyblok_publish"}""", _github.Bodies.Single());
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

    [Theory]
    [InlineData(null, "github-token")]
    [InlineData("s3cret", null)]
    public async Task Without_its_settings_the_webhook_refuses_every_call(string? webhookSecret, string? token)
    {
        DeployTriggerResult result = await Trigger("s3cret", new DeployTriggerOptions { WebhookSecret = webhookSecret, GitHubToken = token });

        Assert.Equal(HttpStatusCode.InternalServerError, result.Status);
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task A_refusal_from_GitHub_is_reported_without_its_details()
    {
        _github.Status = HttpStatusCode.Forbidden;

        DeployTriggerResult result = await Trigger("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
        Assert.Contains("403", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Resource not accessible", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_out_of_reach_is_reported()
    {
        _github.Failure = new HttpRequestException("No such host is known.");

        DeployTriggerResult result = await Trigger("s3cret");

        Assert.Equal(HttpStatusCode.BadGateway, result.Status);
    }

    private async Task<DeployTriggerResult> Trigger(string? secret, DeployTriggerOptions? settings = null)
    {
        using HttpClient http = new(_github, disposeHandler: false);
        DeployTrigger trigger = new(http, Options.Create(settings ?? new DeployTriggerOptions { WebhookSecret = "s3cret", GitHubToken = "github-token" }),
            NullLogger<DeployTrigger>.Instance);
        return await trigger.TriggerAsync(secret, TestContext.Current.CancellationToken);
    }

    // Answers every request the same way and keeps what was sent.
    private sealed class FakeGitHub : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;

        public Exception? Failure { get; set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return Failure is not null
                ? throw Failure
                : new HttpResponseMessage(Status) { Content = new StringContent("""{"message":"Resource not accessible by personal access token"}""", Encoding.UTF8) };
        }
    }
}
