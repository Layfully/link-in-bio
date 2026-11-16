using Client;
using Storyblok;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Prerender;

/// <summary>The <c>wwwroot</c> folder of a <c>dotnet publish</c>, which the prerendered home page is written into.</summary>
public sealed partial class PublishedSite(string wwwroot)
{
    /// <summary>The page as published, before prerendering: what every route except the home page is served.</summary>
    public const string ShellFile = "app-shell.html";

    // index.html marks the loading indicator Blazor replaces; the prerendered page goes there instead.
    private const string StartMarker = "<!--prerender-->";
    private const string EndMarker = "<!--/prerender-->";

    // The config sends certificate pages and every unknown address (with a 404) to the app's page, each by a rewrite.
    private const string RewriteToIndex = "\"rewrite\": \"/index.html\"";

    /// <summary>
    /// Puts <paramref name="html"/> into <c>index.html</c> in place of the loading indicator and the Storyblok responses
    /// (<see cref="PrerenderedStories.ToJson"/>) in a data block the app reads on start. The page as it was becomes
    /// <see cref="ShellFile"/>, and every rewrite in the Static Web Apps config (certificate pages, unknown addresses) points there, so only
    /// <c>/</c> gets the home page's content.
    /// </summary>
    /// <remarks>
    /// Both pages also get the person's photo and profiles from <paramref name="profile"/>, in the link preview
    /// (<c>og:image</c>) and the structured data (<c>application/ld+json</c>), so these follow Storyblok instead of
    /// staying as <c>index.html</c> was written.
    /// </remarks>
    public void WriteHome(string html, string prerenderedJson, SiteProfile profile)
    {
        string indexPath = Path.Combine(wwwroot, "index.html");
        string configPath = Path.Combine(wwwroot, "staticwebapp.config.json");
        string page = WithProfile(File.ReadAllText(indexPath), profile);
        string config = File.ReadAllText(configPath);

        int headEnd = page.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        int start = page.IndexOf(StartMarker, StringComparison.Ordinal);
        int end = page.IndexOf(EndMarker, StringComparison.Ordinal);
        int bodyEnd = page.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (headEnd < 0 || start < headEnd || end < start || bodyEnd < end)
        {
            throw new InvalidOperationException($"{indexPath} has no </head>, or no {StartMarker} … {EndMarker} markers inside its body.");
        }

        // Changed as text, like Csp.targets does, so everything else in the config stays exactly as it was.
        if (!config.Contains(RewriteToIndex, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{configPath} has no {RewriteToIndex} to change.");
        }

        // A data block, not a script: the Content Security Policy doesn't apply to it, and ToJson escapes < and >.
        string data = $"""<script type="application/json" id="{PrerenderedStories.ElementId}">{prerenderedJson}</script>""";
        // The home page's canonical address, which the app adds only once Blazor runs (Home.razor), in the first HTML for
        // crawlers that don't run scripts. Only index.html gets it: it is served for / alone, the shell for every other
        // address. Once Blazor runs the head holds a second, identical link, which search engines accept.
        string headIndent = page[(page.LastIndexOf('\n', headEnd) + 1)..headEnd];
        string newline = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string canonical = $"""    <link rel="canonical" href="{Site.Url}/" />""" + newline + (string.IsNullOrWhiteSpace(headIndent) ? headIndent : string.Empty);

        string home = page[..headEnd] + canonical + page[headEnd..start] + html + page[(end + EndMarker.Length)..bodyEnd] + data + "\n" + page[bodyEnd..];
        // .NET starts seconds after the home page has loaded (js/blazor-start.js), so a connection opened for it at once
        // would close unused first, and Chrome warns about it. Without rel the link does nothing; blazor-start.js makes it
        // a preconnect again when .NET starts. The shell starts .NET at once and keeps it.
        home = PreconnectOnStart().Replace(home, "<link");

        File.WriteAllText(Path.Combine(wwwroot, ShellFile), page);
        File.WriteAllText(indexPath, home);
        File.WriteAllText(configPath, config.Replace(RewriteToIndex, $"\"rewrite\": \"/{ShellFile}\"", StringComparison.Ordinal));

        // Publish compressed the page before it had its content; those copies would serve the page without it.
        foreach (string compressed in new[] { indexPath + ".br", indexPath + ".gz" })
        {
            File.Delete(compressed);
        }
    }

    /// <summary>
    /// The page with the photo and profiles from Storyblok in its link preview and structured data. What the profile
    /// doesn't know (no photo, no links) stays as written, and so does a page without these elements.
    /// </summary>
    private static string WithProfile(string page, SiteProfile profile)
    {
        if (profile.Image is { } image)
        {
            page = PreviewImage().Replace(page, match => match.Groups["start"].Value + WebUtility.HtmlEncode(image) + "\"", 1);
        }

        return StructuredData().Replace(page, match =>
        {
            if (JsonNode.Parse(match.Groups["json"].Value) is not JsonObject data || data["mainEntity"] is not JsonObject person)
            {
                return match.Value;
            }

            if (profile.Image is { } photo)
            {
                person["image"] = photo;
            }

            if (profile.SameAs.Count > 0)
            {
                person["sameAs"] = new JsonArray([.. profile.SameAs.Select(address => JsonValue.Create(address))]);
            }

            // Written as the page has it: indented one step further than the script element, on the page's line endings.
            // The default encoder writes <, > and & as \u escapes, so no address can end the element.
            string indent = match.Groups["indent"].Value;
            string newline = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            IEnumerable<string> lines = data.ToJsonString(IndentedJson).Split('\n').Select(line => indent + "    " + line);
            return match.Groups["start"].Value + newline + string.Join(newline, lines) + newline + indent + "</script>";
        }, 1);
    }

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true, IndentSize = 4, NewLine = "\n" };

    [GeneratedRegex(@"<link\s+rel=""preconnect""(?=[^>]*\sdata-preconnect-on-start[\s/>=])")]
    private static partial Regex PreconnectOnStart();

    [GeneratedRegex(@"(?<start><meta property=""og:image"" content="")[^""]*""")]
    private static partial Regex PreviewImage();

    [GeneratedRegex("""(?<indent>[ \t]*)(?<start><script type="application/ld\+json">)(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex StructuredData();
}
