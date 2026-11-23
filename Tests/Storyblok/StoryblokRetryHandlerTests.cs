using Microsoft.Extensions.Time.Testing;
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
    private readonly HttpClient _sut;

    public StoryblokRetryHandlerTests() => _sut = CreateClient(Policy);

    public void Dispose()
    {
        _sut.Dispose();
        _storyblok.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task SendAsync_ShouldRetry_WhenTheAnswerIsAPassingError(HttpStatusCode status)
    {
        // Arrange
        _storyblok.Answer(status);
        _storyblok.Answer(HttpStatusCode.OK);

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_ShouldRetry_WhenTheConnectionIsBroken()
    {
        // Arrange
        _storyblok.Fail(new HttpRequestException("No connection."));
        _storyblok.Answer(HttpStatusCode.OK);

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task SendAsync_ShouldNotRetry_WhenTheAnswerWouldNotChange(HttpStatusCode status)
    {
        // Arrange
        _storyblok.Answer(status);

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(status);
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnAtOnce_WhenTheAnswerAsksToWaitPastTheTimeLimit()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)));

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_ShouldRetry_WhenTheAnswerAsksToWaitUntilATimeAlreadyPast()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable, new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-1)));
        _storyblok.Answer(HttpStatusCode.OK);

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_ShouldReturnTheLastAnswer_WhenTwoRetriesAreUsedUp()
    {
        // Arrange
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);
        _storyblok.Answer(HttpStatusCode.BadGateway);

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        _storyblok.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_ShouldThrowHttpRequestException_WhenTheConnectionStaysBroken()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            _storyblok.Fail(new HttpRequestException("No connection."));
        }

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(Get);
        _storyblok.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_ShouldRetry_WhenAnAttemptHangs()
    {
        // Arrange
        _storyblok.Hang();
        _storyblok.Answer(HttpStatusCode.OK);

        // Act
        using HttpResponseMessage response = await Get();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_ShouldThrowTimeoutException_WhenStoryblokKeepsHanging()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            _storyblok.Hang();
        }

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(Get);
        _storyblok.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SendAsync_ShouldThrowTimeoutException_WhenTheTotalTimeRunsOutBetweenAttempts()
    {
        // Arrange
        using HttpClient sut = CreateClient(Policy with { TotalTimeout = TimeSpan.FromMilliseconds(150), FirstDelay = TimeSpan.FromSeconds(30) });
        _storyblok.Answer(HttpStatusCode.ServiceUnavailable);

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(() => Get(sut));
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_ShouldThrowOperationCanceledException_WhenTheCallerCancels()
    {
        // Arrange
        _storyblok.Hang();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(30));

        // Act & Assert
        Exception exception = await Should.ThrowAsync<OperationCanceledException>(
            () => Get(cancellationToken: cancellation.Token));

        exception.InnerException.ShouldNotBeOfType<TimeoutException>();
        _storyblok.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendAsync_ShouldReadThePointInTimeOnTheSitesClock_WhenTheAnswerNamesOne()
    {
        // Arrange
        // On the site's clock the time Storyblok named has passed; on the machine's it is years away, which would
        // return the answer instead of asking again.
        FakeTimeProvider time = new(new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero));
        using HttpClient sut = CreateClient(Policy, time);
        _storyblok.Answer(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(time.GetUtcNow().AddSeconds(-1)));
        _storyblok.Answer(HttpStatusCode.OK);

        // Act
        using HttpResponseMessage response = await Get(sut);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _storyblok.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_ShouldWaitOnlyOnce_WhenAConnectionBreaksAfterAPauseStoryblokAskedFor()
    {
        // Arrange
        FakeTimeProvider time = new();
        using HttpClient sut = CreateClient(Policy, time);
        _storyblok.Answer(HttpStatusCode.TooManyRequests, new RetryConditionHeaderValue(TimeSpan.FromSeconds(5)));
        _storyblok.Fail(new HttpRequestException("Connection reset."));
        _storyblok.Answer(HttpStatusCode.OK);

        // Act
        Task<HttpResponseMessage> get = Get(sut);
        await Until(() => _storyblok.Requests.Count == 1);
        time.Advance(TimeSpan.FromSeconds(5));

        // The clock stands still from here: a second five-second pause would never end.
        await Until(() => get.IsCompleted);
        using HttpResponseMessage response = await get;

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _storyblok.Requests.Count.ShouldBe(3);
    }

    // Waits in real time for something the handler does on its own, failing after a second.
    private static async Task Until(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        condition().ShouldBeTrue();
    }

    private Task<HttpResponseMessage> Get() => Get(_sut);

    private Task<HttpResponseMessage> Get(HttpClient? sut = null, CancellationToken? cancellationToken = null) =>
        (sut ?? _sut).GetAsync("https://api.storyblok.com/v2/cdn/stories/home", cancellationToken ?? TestContext.Current.CancellationToken);

    private HttpClient CreateClient(RetryPolicy policy, TimeProvider? time = null) =>
        new(new StoryblokRetryHandler(policy, time ?? TimeProvider.System) { InnerHandler = _storyblok }, disposeHandler: false);
}
