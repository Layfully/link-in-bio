using Client;
using Storyblok;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Prerender;

/// <summary>Writes the prerendered home page into the published <c>wwwroot</c>.</summary>
public sealed partial class PublishedSite(string wwwroot)
{
    /// <summary>The app shell served for non-home routes.</summary>
    public const string ShellFile = "app-shell.html";

    private const string StartMarker = "<!--prerender-->";
    private const string EndMarker = "<!--/prerender-->";

    private const string RewriteToIndex = "\"rewrite\": \"/index.html\"";

    /// <summary>
    /// Writes the rendered HTML and cached stories to <c>index.html</c>, points route rewrites to the app shell,
    /// and updates profile metadata on both pages.
    /// </summary>
    public void WriteHome(string html, string prerenderedJson, SiteProfile profile)
    {
        string indexPath = Path.Combine(wwwroot, "index.html");
        string configPath = Path.Combine(wwwroot, "staticwebapp.config.json");
        string page = WithProfile(File.ReadAllText(indexPath), profile);
        string config = File.ReadAllText(configPath);

        string home = BuildHomePage(page, html, prerenderedJson, indexPath);

        // Replace the rewrite as text to preserve the config's formatting.
        if (!config.Contains(RewriteToIndex, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{configPath} has no {RewriteToIndex} to change.");
        }

        File.WriteAllText(Path.Combine(wwwroot, ShellFile), page);
        File.WriteAllText(indexPath, home);
        File.WriteAllText(configPath, config.Replace(RewriteToIndex, $"\"rewrite\": \"/{ShellFile}\"", StringComparison.Ordinal));

        // The compressed copies still contain the original page.
        File.Delete(indexPath + ".br");
        File.Delete(indexPath + ".gz");
    }

    private static string BuildHomePage(string page, string html, string prerenderedJson, string indexPath)
    {
        int headEnd = page.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        int start = page.IndexOf(StartMarker, StringComparison.Ordinal);
        int end = page.IndexOf(EndMarker, StringComparison.Ordinal);
        int bodyEnd = page.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (headEnd < 0 || start < headEnd || end < start || bodyEnd < end)
        {
            throw new InvalidOperationException($"{indexPath} has no </head>, or no {StartMarker} … {EndMarker} markers inside its body.");
        }

        // PrerenderedStories.ToJson escapes HTML delimiters for this JSON data block.
        string data = $"""<script type="application/json" id="{PrerenderedStories.ElementId}">{prerenderedJson}</script>""";
        // Only the home page gets the root canonical URL.
        ReadOnlySpan<char> template = page.AsSpan();
        ReadOnlySpan<char> headIndent = template[(page.LastIndexOf('\n', headEnd) + 1)..headEnd];
        if (!headIndent.IsWhiteSpace())
        {
            headIndent = [];
        }

        string newline = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string canonical = $"""    <link rel="canonical" href="{Site.Url}/" />""";

        string home = $"{template[..headEnd]}{canonical}{newline}{headIndent}" +
            $"{template[headEnd..start]}{html}{template[(end + EndMarker.Length)..bodyEnd]}" +
            $"{data}\n{template[bodyEnd..]}";
        // The prerendered links work without JavaScript.
        home = NoScript().Replace(home, string.Empty);
        // blazor-start.js restores the preconnect when the runtime starts.
        return PreconnectOnStart().Replace(home, "<link");
    }

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

            // Keep the default JSON encoder so profile URLs cannot close the script element.
            string indent = match.Groups["indent"].Value;
            string newline = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string linePrefix = newline + indent + "    ";
            string json = data.ToJsonString(IndentedJson).Replace("\n", linePrefix, StringComparison.Ordinal);
            return match.Groups["start"].Value + linePrefix + json + newline + indent + "</script>";
        }, 1);
    }

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true, IndentSize = 4, NewLine = "\n" };

    [GeneratedRegex(@"[ \t]*<noscript>.*?</noscript>\r?\n?", RegexOptions.Singleline)]
    private static partial Regex NoScript();

    [GeneratedRegex(@"<link\s+rel=""preconnect""(?=[^>]*\sdata-preconnect-on-start[\s/>=])")]
    private static partial Regex PreconnectOnStart();

    [GeneratedRegex(@"(?<start><meta property=""og:image"" content="")[^""]*""")]
    private static partial Regex PreviewImage();

    [GeneratedRegex("""(?<indent>[ \t]*)(?<start><script type="application/ld\+json">)(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex StructuredData();
}
