namespace Api;

/// <summary>Maps Storyblok story slugs to a GitHub deployment workflow.</summary>
public sealed class DeploySite
{
    public string Repository { get; init; } = string.Empty;
    public string Workflow { get; init; } = string.Empty;
    public string Ref { get; init; } = string.Empty;
    public string[] Slugs { get; init; } = [];
    public string[] SlugPrefixes { get; init; } = [];

    internal bool IsValid =>
        Repository?.Split('/') is [string owner, string name]
        && IsPathSegment(owner) && IsPathSegment(name) && IsPathSegment(Workflow)
        && !string.IsNullOrWhiteSpace(Ref)
        && HasValidSlugs(Slugs) && HasValidSlugs(SlugPrefixes);

    internal bool Builds(string slug) =>
        Slugs.Contains(slug, StringComparer.Ordinal)
        || SlugPrefixes.Any(prefix => slug.StartsWith(prefix, StringComparison.Ordinal));

    private static bool HasValidSlugs(string[]? slugs) => slugs is not null && !Array.Exists(slugs, string.IsNullOrWhiteSpace);

    private static bool IsPathSegment(string? value) =>
        value is { Length: > 0 } and not ("." or "..")
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}
