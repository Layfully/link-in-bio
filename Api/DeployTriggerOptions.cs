namespace Api;

/// <summary>Credentials for the Storyblok deploy webhook.</summary>
public sealed class DeployTriggerOptions
{
    public const string Section = "DeployTrigger";

    /// <summary>Shared secret for incoming webhooks. Set <c>DeployTrigger__WebhookSecret</c> in Azure.</summary>
    public string? WebhookSecret { get; init; }

    /// <summary>GitHub token with Actions write access to the target repositories. Set <c>DeployTrigger__GitHubToken</c> in Azure.</summary>
    public string? GitHubToken { get; init; }
}
