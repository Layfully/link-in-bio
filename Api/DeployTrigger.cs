using Microsoft.Extensions.Options;
using Polly.Timeout;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api;

/// <summary>The HTTP status and message returned to Storyblok.</summary>
public sealed record DeployTriggerResult(HttpStatusCode Status, string Message);

/// <summary>Dispatches GitHub deployment workflows for sites affected by a Storyblok publish.</summary>
public sealed class DeployTrigger(HttpClient http, IOptions<DeployTriggerOptions> options, ILogger<DeployTrigger> logger)
{
    private static readonly DeployTriggerResult NotConfigured = new(HttpStatusCode.InternalServerError, "The webhook isn't set up.");

    /// <param name="secret">The secret from the webhook's address.</param>
    /// <param name="body">What Storyblok sent: <c>{ "action": "published", "full_slug": "home", … }</c>.</param>
    /// <param name="cancellationToken">Ends the call when the host shuts down.</param>
    public Task<DeployTriggerResult> TriggerAsync(string? secret, string? body, CancellationToken cancellationToken) =>
        TriggerFromRequestAsync(secret, _ => Task.FromResult(body), cancellationToken);

    internal async Task<DeployTriggerResult> TriggerFromRequestAsync(string? secret, Func<CancellationToken, Task<string?>> readBodyAsync,
        CancellationToken cancellationToken)
    {
        DeployTriggerOptions settings = options.Value;

        if (string.IsNullOrEmpty(settings.WebhookSecret))
        {
            logger.LogError("{Section}:WebhookSecret isn't set in the application settings.", DeployTriggerOptions.Section);
            return NotConfigured;
        }

        // Authenticate before reading the body or revealing GitHub configuration errors.
        if (!SecretMatches(secret, settings.WebhookSecret))
        {
            logger.LogWarning("A webhook call with a wrong or missing secret was refused.");
            return new(HttpStatusCode.Unauthorized, "Wrong or missing secret.");
        }

        if (settings.Sites is not { Length: > 0 } configuredSites)
        {
            return new(HttpStatusCode.OK, "No deployment targets are configured.");
        }

        if (configuredSites.Any(site => site is not { IsValid: true }))
        {
            logger.LogError("{Section}:Sites contains an invalid deployment target.", DeployTriggerOptions.Section);
            return NotConfigured;
        }

        if (string.IsNullOrEmpty(settings.GitHubToken))
        {
            logger.LogError("{Section}:GitHubToken isn't set in the application settings.", DeployTriggerOptions.Section);
            return NotConfigured;
        }

        // A missing slug deliberately triggers all sites.
        string? slug = ReadStorySlug(await readBodyAsync(cancellationToken));
        DeploySite[] sites = slug is null ? configuredSites : Array.FindAll(configuredSites, site => site.Builds(slug));
        if (sites.Length == 0)
        {
            logger.LogInformation("Story {Slug} isn't part of any site's deploy; nothing started.", slug);
            return new(HttpStatusCode.OK, $"Story {slug} isn't part of any site's deploy; nothing started.");
        }

        (bool Started, string Message)[] outcomes = await Task.WhenAll(
            sites.Select(site => DispatchAsync(site, settings.GitHubToken, cancellationToken)));
        HttpStatusCode status = outcomes.All(outcome => outcome.Started) ? HttpStatusCode.Accepted : HttpStatusCode.BadGateway;
        string message = string.Join(" ", outcomes.Select(outcome => outcome.Message));
        return new(status, message);
    }

    private async Task<(bool Started, string Message)> DispatchAsync(DeploySite site, string token, CancellationToken cancellationToken)
    {
        string address = $"repos/{site.Repository}/actions/workflows/{site.Workflow}/dispatches";
        string dispatch = JsonSerializer.Serialize(new { @ref = site.Ref });

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, address)
            {
                Content = new StringContent(dispatch, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Started the deploy of {Repository} ({Workflow} on {Ref}).", site.Repository, site.Workflow, site.Ref);
                return (true, $"Deploy of {site.Repository} started.");
            }

            // GitHub's answer goes to the log only: it may name the token's permissions.
            logger.LogError("GitHub refused the dispatch to {Repository}: {Status} {Body}", site.Repository, (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken));
            return (false, $"GitHub refused to start the deploy of {site.Repository} ({(int)response.StatusCode}).");
        }
        catch (Exception exception) when (IsUnreachable(exception, cancellationToken))
        {
            logger.LogError(exception, "GitHub couldn't be reached to start the deploy of {Repository}.", site.Repository);
            return (false, $"GitHub couldn't be reached to start the deploy of {site.Repository}.");
        }
    }

    private static string? ReadStorySlug(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("full_slug", out JsonElement slug)
                && slug.ValueKind == JsonValueKind.String
                && slug.GetString()?.Trim('/') is { Length: > 0 } name
                ? name
                : null;
        }
        catch (JsonException)
        {
            // A malformed payload follows the same fallback as a missing slug.
            return null;
        }
    }

    // Caller cancellation must propagate even when an HTTP attempt is eligible for retry.
    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutRejectedException or TaskCanceledException && !cancellationToken.IsCancellationRequested;

    private static bool SecretMatches(string? given, string expected) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
}
