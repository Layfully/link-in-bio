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
    /// A fine-grained GitHub token for <see cref="Repository"/> with Actions: read and write, which lets it start the
    /// workflow and nothing that changes the repository's content.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>The repository whose workflow deploys the site, as owner/name.</summary>
    public string Repository { get; set; } = "Layfully/link-in-bio";

    /// <summary>The workflow file that deploys the site; it must allow <c>workflow_dispatch</c>.</summary>
    public string Workflow { get; set; } = "azure-static-web-apps-gray-tree-0c2e88d03.yml";

    /// <summary>The branch the workflow runs on. Only a run on main deploys production.</summary>
    public string Ref { get; set; } = "main";
}
