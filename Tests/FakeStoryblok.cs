using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LinkInBio.Tests;

/// <summary>Answers requests from a queue of prepared responses and records what was asked.</summary>
internal sealed class FakeStoryblok : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _answers = new();

    public List<Uri> Requests { get; } = [];

    public void Answer(HttpStatusCode status, string body = "{}") =>
        _answers.Enqueue(_ => Task.FromResult(JsonResponse(status, body)));

    /// <summary>An error answer that says when to ask again, as Storyblok does when it limits requests.</summary>
    public void Answer(HttpStatusCode status, RetryConditionHeaderValue retryAfter) =>
        _answers.Enqueue(_ => Task.FromResult(new HttpResponseMessage(status) { Headers = { RetryAfter = retryAfter } }));

    /// <summary>An answer that arrives only when the test completes the returned source, like a slow Storyblok.</summary>
    public TaskCompletionSource AnswerLater(HttpStatusCode status, string body = "{}")
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _answers.Enqueue(async _ =>
        {
            await release.Task;
            return JsonResponse(status, body);
        });
        return release;
    }

    public void Fail(Exception exception) => _answers.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Never answers, like a Storyblok that hangs: the request ends only when it is cancelled.</summary>
    public void Hang() => _answers.Enqueue(async cancellationToken =>
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new UnreachableException();
    });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        return _answers.Dequeue()(cancellationToken);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
