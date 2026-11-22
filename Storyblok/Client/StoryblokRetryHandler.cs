using System.Net;

namespace Storyblok.Client;

/// <summary>Retry limits, timeouts, and backoff for Storyblok requests.</summary>
/// <param name="MaxRetries">Retries after the first attempt.</param>
/// <param name="AttemptTimeout">Time limit for one attempt.</param>
/// <param name="TotalTimeout">Time limit including attempts and delays.</param>
/// <param name="FirstDelay">Initial backoff interval, doubled for each subsequent retry.</param>
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
/// Retries transient Storyblok failures within a total time limit.
/// </summary>
/// <remarks>
/// Every attempt sends the same request message again, which is safe for a GET without a body. A timeout throws
/// <see cref="TimeoutException"/>; the caller's own cancellation stays an <see cref="OperationCanceledException"/>.
/// </remarks>
internal sealed class StoryblokRetryHandler(RetryPolicy policy, TimeProvider time) : DelegatingHandler
{
    public StoryblokRetryHandler(RetryPolicy policy)
        : this(policy, TimeProvider.System)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource totalTimeout = new(policy.TotalTimeout, time);
        using CancellationTokenSource total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, totalTimeout.Token);
        long started = time.GetTimestamp();

        TimeSpan? nextDelay = null;

        for (int retry = 0; ; retry++)
        {
            if (retry > 0)
            {
                try
                {
                    await Task.Delay(nextDelay ?? Max(policy.DelayBefore(retry), TimeSpan.Zero), time, total.Token);
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    throw TimedOut(policy.TotalTimeout, exception);
                }

                nextDelay = null;
            }

            bool isLastAttempt = retry == policy.MaxRetries;
            using CancellationTokenSource attemptTimeout = new(policy.AttemptTimeout, time);
            using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(total.Token, attemptTimeout.Token);

            try
            {
                HttpResponseMessage response = await base.SendAsync(request, attempt.Token);

                if (isLastAttempt || !IsTransient(response.StatusCode))
                {
                    return response;
                }

                // Return the response if its requested delay would exhaust the time budget.
                TimeSpan retryAfter = RetryAfter(response);
                if (retryAfter > TimeSpan.Zero)
                {
                    TimeSpan delay = Max(policy.DelayBefore(retry + 1), retryAfter);
                    if (time.GetElapsedTime(started) + delay >= policy.TotalTimeout)
                    {
                        return response;
                    }

                    nextDelay = delay;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (!isLastAttempt)
            {
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                bool totalExpired = total.IsCancellationRequested;
                if (totalExpired || isLastAttempt)
                {
                    throw TimedOut(totalExpired ? policy.TotalTimeout : policy.AttemptTimeout, exception);
                }
            }
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    // Retry-After is either a number of seconds or a point in time; a time already past means no pause.
    private TimeSpan RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter switch
    {
        { Delta: { } delta } => Max(delta, TimeSpan.Zero),
        { Date: { } date } => Max(date - time.GetUtcNow(), TimeSpan.Zero),
        _ => TimeSpan.Zero,
    };

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeoutException TimedOut(TimeSpan timeout, Exception exception) =>
        new($"Storyblok didn't answer within {timeout.TotalSeconds:0.###} seconds.", exception);
}
