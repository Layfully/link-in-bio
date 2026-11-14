using System.Diagnostics;
using System.Net;

namespace Storyblok.Client;

/// <summary>How often and how long a Storyblok request is tried.</summary>
/// <param name="MaxRetries">Retries after the first attempt.</param>
/// <param name="AttemptTimeout">How long one attempt may take before it counts as failed and is retried.</param>
/// <param name="TotalTimeout">How long a page waits at most, retries and the pauses between them included.</param>
/// <param name="FirstDelay">The pause before the first retry; it doubles before each further one.</param>
internal sealed record RetryPolicy(int MaxRetries, TimeSpan AttemptTimeout, TimeSpan TotalTimeout, TimeSpan FirstDelay)
{
    public static RetryPolicy Default { get; } = new(
        MaxRetries: 2,
        AttemptTimeout: TimeSpan.FromSeconds(5),
        TotalTimeout: TimeSpan.FromSeconds(15),
        FirstDelay: TimeSpan.FromMilliseconds(500));

    public TimeSpan DelayBefore(int retry) => FirstDelay * Math.Pow(2, retry - 1);
}

/// <summary>
/// Retries Storyblok requests that failed for a passing reason (no connection, a timeout, 408, 429 or a 5xx answer) and
/// caps how long a page waits when Storyblok hangs. A few lines instead of Polly, which would add about 100 KB of
/// assemblies to every visitor's download for a single GET request.
/// </summary>
/// <remarks>
/// Every attempt sends the same request message again, which is safe for a GET without a body. A timeout throws
/// <see cref="TimeoutException"/>; the caller's own cancellation stays an <see cref="OperationCanceledException"/>.
/// </remarks>
internal sealed class StoryblokRetryHandler(RetryPolicy policy) : DelegatingHandler
{
    public StoryblokRetryHandler()
        : this(RetryPolicy.Default)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(policy.TotalTimeout);
        long started = Stopwatch.GetTimestamp();

        // The pause Storyblok asked for with its last answer, if any.
        TimeSpan retryAfter = TimeSpan.Zero;

        for (int retry = 0; ; retry++)
        {
            if (retry > 0)
            {
                try
                {
                    await Task.Delay(Max(policy.DelayBefore(retry), retryAfter), total.Token);
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    throw TimedOut(policy.TotalTimeout, exception);
                }
            }

            bool isLastAttempt = retry == policy.MaxRetries;
            using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
            attempt.CancelAfter(policy.AttemptTimeout);

            try
            {
                HttpResponseMessage response = await base.SendAsync(request, attempt.Token);

                if (isLastAttempt || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                // Storyblok may say how long to wait, usually when it limits requests (429). Asking sooner would only
                // be refused again, and a pause that ends past the whole request's limit would only end in a timeout,
                // so its answer is returned instead.
                retryAfter = RetryAfter(response);
                if (retryAfter > TimeSpan.Zero
                    && Stopwatch.GetElapsedTime(started) + Max(policy.DelayBefore(retry + 1), retryAfter) >= policy.TotalTimeout)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (!isLastAttempt)
            {
                // No connection, or the connection broke: the next attempt may get through.
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Not the caller's cancellation, so a timeout: the whole request's ends it, one attempt's is retried.
                if (total.IsCancellationRequested)
                {
                    throw TimedOut(policy.TotalTimeout, exception);
                }

                if (isLastAttempt)
                {
                    throw TimedOut(policy.AttemptTimeout, exception);
                }
            }
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    // Retry-After is either a number of seconds or a point in time; a time already past means no pause.
    private static TimeSpan RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => Max(delta, TimeSpan.Zero),
        { Date: { } date } => Max(date - DateTimeOffset.UtcNow, TimeSpan.Zero),
        _ => TimeSpan.Zero,
    };

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeoutException TimedOut(TimeSpan timeout, Exception exception) =>
        new($"Storyblok didn't answer within {timeout.TotalSeconds:0.###} seconds.", exception);
}
