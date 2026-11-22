using Client;
using Storyblok;
using System.Globalization;
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
        ReadOnlySpan<char> template = page.AsSpan();
        string canonical = HeadTags(page, headEnd, [$"""<link rel="canonical" href="{Site.Url}/" />"""]);

        string home = $"{template[..headEnd]}{canonical}" +
            $"{template[headEnd..start]}{html}{template[(end + EndMarker.Length)..bodyEnd]}" +
            $"{data}\n{template[bodyEnd..]}";
        // The prerendered links work without JavaScript.
        home = NoScript().Replace(home, string.Empty);
        // blazor-start.js restores the preconnect when the runtime starts.
        return PreconnectOnStart().Replace(home, "<link");
    }

    private static string WithProfile(string page, SiteProfile profile)
    {
        string newline = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (profile.Title is { } title)
        {
            page = PageTitle().Replace(page, _ => $"<title>{WebUtility.HtmlEncode(title)}</title>", 1);
        }

        page = ProfileMeta().Replace(page, match =>
        {
            string? value = match.Groups["key"].Value switch
            {
                "description" or "og:description" => profile.Description,
                "og:title" => profile.Title,
                "og:image" => profile.Image,
                "og:image:alt" => profile.ImageAlt,
                _ => null,
            };
            return value is null ? match.Value : match.Groups["start"].Value + WebUtility.HtmlEncode(value) + "\"";
        });

        if (profile.Image is { } image
            && !page.Contains("<meta property=\"og:image\"", StringComparison.Ordinal)
            && page.IndexOf("</head>", StringComparison.OrdinalIgnoreCase) is >= 0 and var headEnd)
        {
            string size = SiteProfile.ImageSize.ToString(CultureInfo.InvariantCulture);
            List<string> imageMetadata =
            [
                OpenGraphTag("og:image", image),
                OpenGraphTag("og:image:width", size),
                OpenGraphTag("og:image:height", size),
            ];
            if (profile.ImageAlt is { } alt)
            {
                imageMetadata.Add(OpenGraphTag("og:image:alt", alt));
            }

            page = page.Insert(headEnd, HeadTags(page, headEnd, imageMetadata));
        }

        return StructuredData().Replace(page, match =>
        {
            if (JsonNode.Parse(match.Groups["json"].Value) is not JsonObject data)
            {
                return match.Value;
            }

            if (data["mainEntity"] is JsonObject person)
            {
                if (profile.Name is not null)
                {
                    person.Remove("alternateName");
                }

                foreach ((string key, string? value) in new (string, string?)[]
                {
                    ("name", profile.Name),
                    ("jobTitle", profile.JobTitle),
                    ("description", profile.Description),
                    ("image", profile.Image),
                })
                {
                    if (value is not null)
                    {
                        person[key] = value;
                    }
                }

                if (profile.SameAs.Count > 0)
                {
                    person["sameAs"] = new JsonArray([.. profile.SameAs.Select(address => JsonValue.Create(address))]);
                }
            }
            else if ((string?)data["@type"] == "WebSite" && profile.Name is { } siteName)
            {
                data["name"] = siteName;
            }
            else
            {
                return match.Value;
            }

            // Keep the default JSON encoder so profile URLs cannot close the script element.
            string indent = match.Groups["indent"].Value;
            string linePrefix = newline + indent + "    ";
            string json = data.ToJsonString(IndentedJson).Replace("\n", linePrefix, StringComparison.Ordinal);
            return match.Groups["start"].Value + linePrefix + json + newline + indent + "</script>";
        });
    }

    private static string HeadTags(string page, int headEnd, IReadOnlyList<string> tags)
    {
        ReadOnlySpan<char> line = page.AsSpan((page.LastIndexOf('\n', headEnd) + 1)..headEnd);
        string indent = line.IsWhiteSpace() ? line.ToString() : string.Empty;
        string newline = page.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return "    " + string.Join(newline + indent + "    ", tags) + newline + indent;
    }

    private static string OpenGraphTag(string property, string value) =>
        $"""<meta property="{property}" content="{WebUtility.HtmlEncode(value)}" />""";

    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true, IndentSize = 4, NewLine = "\n" };

    [GeneratedRegex(@"[ \t]*<noscript>.*?</noscript>\r?\n?", RegexOptions.Singleline)]
    private static partial Regex NoScript();

    [GeneratedRegex(@"<link\s+rel=""preconnect""(?=[^>]*\sdata-preconnect-on-start[\s/>=])")]
    private static partial Regex PreconnectOnStart();

    [GeneratedRegex("<title>[^<]*</title>")]
    private static partial Regex PageTitle();

    [GeneratedRegex(@"(?<start><meta (?:name|property)=""(?<key>description|og:title|og:description|og:image|og:image:alt)""\s+content="")[^""]*""")]
    private static partial Regex ProfileMeta();

    [GeneratedRegex("""(?<indent>[ \t]*)(?<start><script type="application/ld\+json">)(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex StructuredData();
}
