using Storyblok.Client;
using System.Net;
using System.Net.Http.Headers;

namespace LinkInBio.Tests.Storyblok;

/// <summary>Which failures the Storyblok requests retry, and how the timeouts end a request that hangs.</summary>
public sealed class StoryblokRetryHandlerTests : IDisposable
{
    // Short timeouts and no pauses, so a test that waits for a timeout takes a fraction of a second.
    private static readonly RetryPolicy Policy = new(
        MaxRetries: 2,
        AttemptTimeout: TimeSpan.FromMilliseconds(100),
        TotalTimeout: TimeSpan.FromSeconds(10),
        FirstDelay: TimeSpan.Zero);

    private readonly FakeStoryblok _storyblok = new();
    private readonly HttpClient _client;

    public StoryblokRetryHandlerTests() => _client = CreateClient(Policy);

    public void Dispose()
    {
        _client.Dispose();
        _storyblok.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task A_passing_error_answer_is_tried_again(HttpStatusCode status)
    {
        _storyblok.Answer(status);
        _storyblok.Answer(HttpStatusCode.OK);

        using HttpResponseMessage response = await Get();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task A_broken_connection_is_tried_again()
    {
        _storyblok.Fail(new HttpRequestException("No connection."));
        _storyblok.Answer(HttpStatusCode.OK);

        using HttpResponseMessage response = await Get();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task An_answer_that_wouldnt_change_is_not_tried_again(HttpStatusCode status)
    {
        _storyblok.Answer(status);

        using HttpResponseMessage response = await Get();

        Assert.Equal(status, response.StatusCode);
        Assert.Single(_storyblok.Requests);
    }

    [Fact]
    public async Task An_answer_asking_to_wait_past_the_time_limit_is_returned_at_once()
    {
        _storyblok.Answer(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)));

        using HttpResponseMessage response = await Get();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Single(_storyblok.Requests);
    }

    [Fact]
    public async Task An_answer_asking_to_wait_until_a_time_already_past_is_tried_again()
    {
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-1)));
        _storyblok.Answer(HttpStatusCode.OK);

        using HttpResponseMessage response = await Get();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task After_two_retries_the_last_answer_is_returned()
    {
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);
        _storyblok.Answer(HttpStatusCode.BadGateway);

        using HttpResponseMessage response = await Get();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(3, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task After_two_retries_a_broken_connection_is_the_error()
    {
        for (int i = 0; i < 3; i++)
        {
            _storyblok.Fail(new HttpRequestException("No connection."));
        }

        await Assert.ThrowsAsync<HttpRequestException>(Get);
        Assert.Equal(3, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task An_attempt_that_hangs_is_given_up_and_tried_again()
    {
        _storyblok.Hang();
        _storyblok.Answer(HttpStatusCode.OK);

        using HttpResponseMessage response = await Get();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_Storyblok_that_keeps_hanging_times_out()
    {
        for (int i = 0; i < 3; i++)
        {
            _storyblok.Hang();
        }

        await Assert.ThrowsAsync<TimeoutException>(Get);
        Assert.Equal(3, _storyblok.Requests.Count);
    }

    [Fact]
    public async Task The_whole_request_times_out_even_between_attempts()
    {
        using HttpClient client = CreateClient(Policy with { TotalTimeout = TimeSpan.FromMilliseconds(150), FirstDelay = TimeSpan.FromSeconds(30) });
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<TimeoutException>(() => client.GetAsync("https://api.storyblok.com/v2/cdn/stories/home", TestContext.Current.CancellationToken));
        Assert.Single(_storyblok.Requests);
    }

    [Fact]
    public async Task The_callers_cancellation_is_not_a_timeout()
    {
        _storyblok.Hang();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(30));

        Exception exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _client.GetAsync("https://api.storyblok.com/v2/cdn/stories/home", cancellation.Token));

        Assert.IsNotType<TimeoutException>(exception.InnerException);
        Assert.Single(_storyblok.Requests);
    }

    private Task<HttpResponseMessage> Get() =>
        _client.GetAsync("https://api.storyblok.com/v2/cdn/stories/home", TestContext.Current.CancellationToken);

    private HttpClient CreateClient(RetryPolicy policy) =>
        new(new StoryblokRetryHandler(policy) { InnerHandler = _storyblok }, disposeHandler: false);
}
