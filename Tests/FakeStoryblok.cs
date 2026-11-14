using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LinkInBio.Tests;

/// <summary>Answers requests from a queue of prepared responses and records what was asked.</summary>
internal sealed class FakeStoryblok : HttpMessageHandler, IHttpClientFactory
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _answers = new();

    public List<Uri> Requests { get; } = [];

    public void Answer(HttpStatusCode status, string body = "{}") =>
        _answers.Enqueue(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    /// <summary>An error answer that says when to ask again, as Storyblok does when it limits requests.</summary>
    public void Answer(HttpStatusCode status, RetryConditionHeaderValue retryAfter) =>
        _answers.Enqueue(_ => Task.FromResult(new HttpResponseMessage(status) { Headers = { RetryAfter = retryAfter } }));

    public void Fail(Exception exception) => _answers.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Never answers, like a Storyblok that hangs: the request ends only when it is cancelled.</summary>
    public void Hang() => _answers.Enqueue(async cancellationToken =>
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    });

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        return _answers.Dequeue()(cancellationToken);
    }
}
