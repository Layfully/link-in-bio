using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api;

/// <summary>What the webhook answers Storyblok, which shows it in the webhook's log.</summary>
public sealed record DeployTriggerResult(HttpStatusCode Status, string Message);

/// <summary>
/// Starts the site's deploy when Storyblok reports a publish: GitHub's <c>workflow_dispatch</c>, the same as "Run
/// workflow" in the Actions tab. Storyblok's webhooks can't send the Authorization header GitHub needs, so the token
/// stays here. It needs only Actions: write, which can start runs but not change the repository.
/// </summary>
/// <remarks>Without the Functions types, so tests can call it directly; <see cref="StoryblokWebhook"/> is the endpoint.</remarks>
public sealed class DeployTrigger(HttpClient http, IOptions<DeployTriggerOptions> options, TimeProvider time, ILogger<DeployTrigger> logger)
{
    /// <summary>
    /// Attempts at GitHub, the first included. Storyblok never repeats a webhook call that failed, so a passing failure
    /// here (a 5xx answer, a broken connection, a timeout) would otherwise lose the deploy of that publish.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>The pause before the second attempt; it doubles before each further one.</summary>
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(1);

    public async Task<DeployTriggerResult> TriggerAsync(string? secret, CancellationToken cancellationToken)
    {
        DeployTriggerOptions settings = options.Value;

        // Without a secret no call can be told apart from a stranger's.
        if (string.IsNullOrEmpty(settings.WebhookSecret))
        {
            logger.LogError("{Section}:WebhookSecret isn't set in the application settings.", DeployTriggerOptions.Section);
            return new(HttpStatusCode.InternalServerError, "The webhook isn't set up.");
        }

        // Before anything else is said about the setup: a stranger learns nothing from the answer.
        if (!IsSecret(secret, settings.WebhookSecret))
        {
            logger.LogWarning("A webhook call with a wrong or missing secret was refused.");
            return new(HttpStatusCode.Unauthorized, "Wrong or missing secret.");
        }

        if (string.IsNullOrEmpty(settings.GitHubToken))
        {
            logger.LogError("{Section}:GitHubToken isn't set in the application settings.", DeployTriggerOptions.Section);
            return new(HttpStatusCode.InternalServerError, "The webhook isn't set up.");
        }

        string address = $"https://api.github.com/repos/{settings.Repository}/actions/workflows/{settings.Workflow}/dispatches";
        string body = JsonSerializer.Serialize(new { @ref = settings.Ref });

        for (int attempt = 1; ; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(FirstDelay * Math.Pow(2, attempt - 2), time, cancellationToken);
            }

            bool isLastAttempt = attempt == MaxAttempts;

            try
            {
                using HttpResponseMessage response = await http.SendAsync(Request(address, body, settings.GitHubToken), cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Started the deploy of {Repository} ({Workflow} on {Ref}).", settings.Repository, settings.Workflow, settings.Ref);
                    return new(HttpStatusCode.Accepted, $"Deploy of {settings.Repository} started.");
                }

                if (!isLastAttempt && IsTransient(response.StatusCode))
                {
                    logger.LogWarning("GitHub answered {Status} to the dispatch; trying again.", (int)response.StatusCode);
                    continue;
                }

                // GitHub's answer goes to the log only: it may name the token's permissions.
                logger.LogError("GitHub refused the dispatch to {Repository}: {Status} {Body}", settings.Repository, (int)response.StatusCode,
                    await response.Content.ReadAsStringAsync(cancellationToken));
                return new(HttpStatusCode.BadGateway, $"GitHub refused to start the deploy ({(int)response.StatusCode}).");
            }
            catch (Exception exception) when (IsUnreachable(exception, cancellationToken))
            {
                if (!isLastAttempt)
                {
                    logger.LogWarning(exception, "GitHub couldn't be reached for the dispatch; trying again.");
                    continue;
                }

                logger.LogError(exception, "GitHub couldn't be reached to start the deploy of {Repository}.", settings.Repository);
                return new(HttpStatusCode.BadGateway, "GitHub couldn't be reached.");
            }
        }
    }

    // A message can be sent only once, so every attempt gets its own.
    private static HttpRequestMessage Request(string address, string body, string token)
    {
        HttpRequestMessage request = new(HttpMethod.Post, address)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("link-in-bio-deploy-trigger", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    // No connection, or HttpClient's own timeout (a TaskCanceledException). The caller's cancellation is neither: the
    // host is shutting down, so it propagates.
    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken) =>
        (exception is HttpRequestException || exception is TaskCanceledException) && !cancellationToken.IsCancellationRequested;

    // Compared in constant time, so the response time doesn't tell how much of a guess was right.
    private static bool IsSecret(string? given, string expected) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
}
