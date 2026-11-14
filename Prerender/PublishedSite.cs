using Storyblok;

namespace Prerender;

/// <summary>The <c>wwwroot</c> folder of a <c>dotnet publish</c>, which the prerendered home page is written into.</summary>
public sealed class PublishedSite(string wwwroot)
{
    /// <summary>The page as published, before prerendering: what every route except the home page is served.</summary>
    public const string ShellFile = "app-shell.html";

    // index.html marks the loading indicator Blazor replaces; the prerendered page goes there instead.
    private const string StartMarker = "<!--prerender-->";
    private const string EndMarker = "<!--/prerender-->";

    private const string FallbackToIndex = "\"rewrite\": \"/index.html\"";

    /// <summary>
    /// Puts <paramref name="html"/> into <c>index.html</c> in place of the loading indicator and the Storyblok responses
    /// (<see cref="PrerenderedStories.ToJson"/>) in a data block the app reads on start. The page as it was becomes
    /// <see cref="ShellFile"/>, and the Static Web Apps config sends every route without a file of its own there, so only
    /// <c>/</c> gets the home page's content.
    /// </summary>
    public void WriteHome(string html, string prerenderedJson)
    {
        string indexPath = Path.Combine(wwwroot, "index.html");
        string configPath = Path.Combine(wwwroot, "staticwebapp.config.json");
        string page = File.ReadAllText(indexPath);
        string config = File.ReadAllText(configPath);

        int start = page.IndexOf(StartMarker, StringComparison.Ordinal);
        int end = page.IndexOf(EndMarker, StringComparison.Ordinal);
        int bodyEnd = page.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end < start || bodyEnd < end)
        {
            throw new InvalidOperationException($"{indexPath} has no {StartMarker} … {EndMarker} markers inside its body.");
        }

        // Changed as text, like Csp.targets does, so everything else in the config stays exactly as it was.
        if (!config.Contains(FallbackToIndex, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{configPath} has no {FallbackToIndex} navigation fallback to change.");
        }

        // A data block, not a script: the Content Security Policy doesn't apply to it, and ToJson escapes < and >.
        string data = $"""<script type="application/json" id="{PrerenderedStories.ElementId}">{prerenderedJson}</script>""";
        string home = page[..start] + html + page[(end + EndMarker.Length)..bodyEnd] + data + "\n" + page[bodyEnd..];

        File.WriteAllText(Path.Combine(wwwroot, ShellFile), page);
        File.WriteAllText(indexPath, home);
        File.WriteAllText(configPath, config.Replace(FallbackToIndex, $"\"rewrite\": \"/{ShellFile}\"", StringComparison.Ordinal));

        // Publish compressed the page before it had its content; those copies would serve the page without it.
        foreach (string compressed in new[] { indexPath + ".br", indexPath + ".gz" })
        {
            File.Delete(compressed);
        }
    }
}
