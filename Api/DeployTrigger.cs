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
/// Starts the site's deploy when Storyblok reports a publish: GitHub's <c>repository_dispatch</c> event, which the
/// workflow listens for. Storyblok's webhooks can't send the Authorization header GitHub needs, so the token stays here.
/// </summary>
/// <remarks>Without the Functions types, so tests can call it directly; <see cref="StoryblokWebhook"/> is the endpoint.</remarks>
public sealed class DeployTrigger(HttpClient http, IOptions<DeployTriggerOptions> options, ILogger<DeployTrigger> logger)
{
    public async Task<DeployTriggerResult> TriggerAsync(string? secret, CancellationToken cancellationToken)
    {
        DeployTriggerOptions settings = options.Value;

        if (string.IsNullOrEmpty(settings.WebhookSecret) || string.IsNullOrEmpty(settings.GitHubToken))
        {
            logger.LogError("{Section}:WebhookSecret or {Section}:GitHubToken isn't set in the application settings.", DeployTriggerOptions.Section, DeployTriggerOptions.Section);
            return new(HttpStatusCode.InternalServerError, "The webhook isn't set up.");
        }

        if (!IsSecret(secret, settings.WebhookSecret))
        {
            logger.LogWarning("A webhook call with a wrong or missing secret was refused.");
            return new(HttpStatusCode.Unauthorized, "Wrong or missing secret.");
        }

        using HttpRequestMessage request = new(HttpMethod.Post, $"https://api.github.com/repos/{settings.Repository}/dispatches")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { event_type = settings.EventType }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.GitHubToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("link-in-bio-deploy-trigger", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Started the deploy of {Repository} ({EventType}).", settings.Repository, settings.EventType);
                return new(HttpStatusCode.Accepted, $"Deploy of {settings.Repository} started.");
            }

            // GitHub's answer goes to the log only: it may name the token's permissions.
            logger.LogError("GitHub refused the dispatch to {Repository}: {Status} {Body}", settings.Repository, (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken));
            return new(HttpStatusCode.BadGateway, $"GitHub refused to start the deploy ({(int)response.StatusCode}).");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "GitHub couldn't be reached to start the deploy of {Repository}.", settings.Repository);
            return new(HttpStatusCode.BadGateway, "GitHub couldn't be reached.");
        }
    }

    // Compared in constant time, so the response time doesn't tell how much of a guess was right.
    private static bool IsSecret(string? given, string expected) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
}
