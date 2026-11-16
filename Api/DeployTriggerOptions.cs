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

    /// <summary>
    /// A fine-grained GitHub token for the repositories in <see cref="DeploySite.All"/> with Actions: read and write,
    /// which lets it start their workflows and nothing that changes the repositories' content.
    /// </summary>
    public string? GitHubToken { get; set; }
}
