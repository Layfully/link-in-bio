namespace Api;

/// <summary>Maps Storyblok story slugs to a GitHub deployment workflow.</summary>
/// <param name="Repository">The GitHub repository in owner/name format.</param>
/// <param name="Workflow">The workflow file that accepts <c>workflow_dispatch</c>.</param>
/// <param name="Ref">The branch the workflow runs on.</param>
/// <param name="Builds">Tests whether the build includes a story's full slug.</param>
public sealed record DeploySite(string Repository, string Workflow, string Ref, Func<string, bool> Builds)
{
    /// <summary>Only the home story is prerendered at deploy time.</summary>
    public static DeploySite LinkInBio { get; } = new(
        "Layfully/link-in-bio", "azure-static-web-apps-gray-tree-0c2e88d03.yml", "main", slug => slug == "home");

    /// <summary>Matches the portfolio build's <c>starts_with</c> filter, including English and Polish slugs.</summary>
    public static DeploySite Portfolio { get; } = new(
        "Layfully/portfolio", "azure-static-web-apps-purple-tree-0510d5403.yml", "main", IsPortfolioStory);

    public static IReadOnlyList<DeploySite> All { get; } = [LinkInBio, Portfolio];

    private static bool IsPortfolioStory(string slug) =>
        slug.StartsWith("portfolio", StringComparison.Ordinal) ||
        slug.StartsWith("en/portfolio", StringComparison.Ordinal) ||
        slug.StartsWith("pl/portfolio", StringComparison.Ordinal);
}
