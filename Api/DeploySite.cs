namespace Api;

/// <summary>
/// A site built from the Storyblok space, and which of the space's stories go into its build. The space holds the
/// stories of this site and of the portfolio, and the Storyblok plan allows two webhooks, so one webhook (this API)
/// starts the builds of both.
/// </summary>
/// <param name="Repository">The repository whose workflow builds and deploys the site, as owner/name.</param>
/// <param name="Workflow">The workflow file; it must allow <c>workflow_dispatch</c> and deploy production from <see cref="Ref"/>.</param>
/// <param name="Ref">The branch the workflow runs on.</param>
/// <param name="Builds">True for a story's full slug (<c>home</c>, <c>pl/portfolio</c>) that goes into the site's build.</param>
public sealed record DeploySite(string Repository, string Workflow, string Ref, Func<string, bool> Builds)
{
    /// <summary>
    /// This site: only the home story is built into it at deploy time (Prerender). Certificates load in the browser,
    /// so publishing one needs no deploy.
    /// </summary>
    public static DeploySite LinkInBio { get; } = new(
        "Layfully/link-in-bio", "azure-static-web-apps-gray-tree-0c2e88d03.yml", "main", slug => slug == "home");

    /// <summary>
    /// The portfolio: its build fetches the stories that start with "portfolio" (<c>starts_with</c> in its
    /// scripts/fetch-storyblok-data.mjs), in English and Polish, which a folder-level translation puts before the slug.
    /// </summary>
    public static DeploySite Portfolio { get; } = new(
        "Layfully/portfolio", "azure-static-web-apps-purple-tree-0510d5403.yml", "main", IsPortfolioStory);

    public static IReadOnlyList<DeploySite> All { get; } = [LinkInBio, Portfolio];

    private static bool IsPortfolioStory(string slug)
    {
        foreach (string language in (string[])["en/", "pl/"])
        {
            if (slug.StartsWith(language, StringComparison.Ordinal))
            {
                slug = slug[language.Length..];
                break;
            }
        }

        return slug.StartsWith("portfolio", StringComparison.Ordinal);
    }
}
