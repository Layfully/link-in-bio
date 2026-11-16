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
/// Starts the deploy of each site the published story goes into (<see cref="DeploySite"/>): GitHub's
/// <c>workflow_dispatch</c>, the same as "Run workflow" in the Actions tab. Storyblok's webhooks can't send the
/// Authorization header GitHub needs, so the token stays here. It needs only Actions: write on the sites' repositories,
/// which can start runs but not change the repositories.
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

    /// <param name="secret">The secret from the webhook's address.</param>
    /// <param name="body">What Storyblok sent: <c>{ "action": "published", "full_slug": "home", … }</c>.</param>
    /// <param name="cancellationToken">Ends the call when the host shuts down.</param>
    public async Task<DeployTriggerResult> TriggerAsync(string? secret, string? body, CancellationToken cancellationToken)
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

        // A call that doesn't say which story it is about deploys every site, so a change in what Storyblok sends can't
        // stop deploys.
        string? slug = StorySlug(body);
        DeploySite[] sites = [.. DeploySite.All.Where(site => slug is null || site.Builds(slug))];
        if (sites.Length == 0)
        {
            logger.LogInformation("Story {Slug} isn't part of any site's deploy; nothing started.", slug);
            return new(HttpStatusCode.OK, $"Story {slug} isn't part of any site's deploy; nothing started.");
        }

        // At the same time, so trying several sites again stays within the time Static Web Apps gives the request
        // (Program.cs).
        (bool Started, string Message)[] outcomes = await Task.WhenAll(sites.Select(site => DispatchAsync(site, settings.GitHubToken, cancellationToken)));
        string message = string.Join(" ", outcomes.Select(outcome => outcome.Message));
        return new(outcomes.All(outcome => outcome.Started) ? HttpStatusCode.Accepted : HttpStatusCode.BadGateway, message);
    }

    private async Task<(bool Started, string Message)> DispatchAsync(DeploySite site, string token, CancellationToken cancellationToken)
    {
        string address = $"https://api.github.com/repos/{site.Repository}/actions/workflows/{site.Workflow}/dispatches";
        string dispatch = JsonSerializer.Serialize(new { @ref = site.Ref });

        for (int attempt = 1; ; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(FirstDelay * Math.Pow(2, attempt - 2), time, cancellationToken);
            }

            bool isLastAttempt = attempt == MaxAttempts;

            try
            {
                using HttpResponseMessage response = await http.SendAsync(Request(address, dispatch, token), cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Started the deploy of {Repository} ({Workflow} on {Ref}).", site.Repository, site.Workflow, site.Ref);
                    return (true, $"Deploy of {site.Repository} started.");
                }

                if (!isLastAttempt && IsTransient(response.StatusCode))
                {
                    logger.LogWarning("GitHub answered {Status} to the dispatch to {Repository}; trying again.", (int)response.StatusCode, site.Repository);
                    continue;
                }

                // GitHub's answer goes to the log only: it may name the token's permissions.
                logger.LogError("GitHub refused the dispatch to {Repository}: {Status} {Body}", site.Repository, (int)response.StatusCode,
                    await response.Content.ReadAsStringAsync(cancellationToken));
                return (false, $"GitHub refused to start the deploy of {site.Repository} ({(int)response.StatusCode}).");
            }
            catch (Exception exception) when (IsUnreachable(exception, cancellationToken))
            {
                if (!isLastAttempt)
                {
                    logger.LogWarning(exception, "GitHub couldn't be reached for the dispatch to {Repository}; trying again.", site.Repository);
                    continue;
                }

                logger.LogError(exception, "GitHub couldn't be reached to start the deploy of {Repository}.", site.Repository);
                return (false, $"GitHub couldn't be reached to start the deploy of {site.Repository}.");
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

    // The story's full slug from the webhook's body, without slashes around it; null when the body doesn't name one.
    private static string? StorySlug(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("full_slug", out JsonElement slug)
                && slug.ValueKind == JsonValueKind.String
                && slug.GetString()?.Trim('/') is { Length: > 0 } name
                    ? name
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
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
