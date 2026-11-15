namespace Api;

/// <summary>
/// How a publish in Storyblok reaches GitHub. Set in the Static Web App's application settings, under
/// <c>DeployTrigger__…</c> (README, Deployment).
/// </summary>
public sealed class DeployTriggerOptions
{
    public const string Section = "DeployTrigger";

    /// <summary>The secret in the webhook's address (<c>?secret=…</c>), which keeps strangers from starting deploys.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>A fine-grained GitHub token with Contents: read and write on <see cref="Repository"/>.</summary>
    public string? GitHubToken { get; set; }

    /// <summary>The repository whose workflow deploys the site, as owner/name.</summary>
    public string Repository { get; set; } = "Layfully/link-in-bio";

    /// <summary>The <c>repository_dispatch</c> event type the workflow listens for.</summary>
    public string EventType { get; set; } = "storyblok_publish";
}
