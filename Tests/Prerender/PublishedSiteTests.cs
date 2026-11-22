using Prerender;
using System.Text.Json.Nodes;

namespace LinkInBio.Tests.Prerender;

/// <summary>How the prerendered home page is written into a published site.</summary>
public sealed class PublishedSiteTests : IDisposable
{
    private const string Index = """
        <html>
            <head>
                <title>Links</title>
            </head>
        <body>
        <div id="app"><!--prerender--><p>Loading</p><!--/prerender--></div>
        <div id="analytics-consent" hidden></div>
        <script src="_framework/blazor.webassembly.js"></script>
        </body></html>
        """;

    private const string Config = """
        {
          "routes": [
            { "route": "/Certificate/*", "rewrite": "/index.html" }
          ],
          "responseOverrides": {
            "404": { "rewrite": "/index.html", "statusCode": 404 }
          },
          "globalHeaders": {
            "Content-Security-Policy": "default-src 'self'; script-src 'self' 'sha256-abc+def='"
          }
        }
        """;

    private readonly string _wwwroot = Directory.CreateTempSubdirectory("prerender-").FullName;

    public PublishedSiteTests()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), Index);
        File.WriteAllText(Path.Combine(_wwwroot, "staticwebapp.config.json"), Config);
        File.WriteAllText(Path.Combine(_wwwroot, "robots.txt"), "User-agent: *\nAllow: /\n");
    }

    public void Dispose() => Directory.Delete(_wwwroot, recursive: true);

    private void WriteHome(string stories = "{}", SiteProfile? profile = null) =>
        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", stories, profile ?? SiteProfile.None);

    [Fact]
    public void The_home_page_is_written_between_the_markers_and_the_shell_is_kept()
    {
        WriteHome("""{"home":{}}""");

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""<div id="app"><main>Links</main></div>""", index, StringComparison.Ordinal);
        Assert.Contains("""<script type="application/json" id="storyblok-prerendered">{"home":{}}</script>""", index, StringComparison.Ordinal);
        Assert.DoesNotContain("Loading", index, StringComparison.Ordinal);
        Assert.Equal(Index, File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")));
        Assert.DoesNotContain("rel=\"canonical\"", index, StringComparison.Ordinal);
        Assert.DoesNotContain("data-measurement-id", index, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_wwwroot, "sitemap.xml")));
        Assert.DoesNotContain("Sitemap:", File.ReadAllText(Path.Combine(_wwwroot, "robots.txt")), StringComparison.Ordinal);
    }

    [Fact]
    public void Both_pages_get_analytics_settings_from_the_profile()
    {
        WriteHome(profile: SiteProfile.None with
        {
            AnalyticsMeasurementId = "G-EXAMPLE123",
            AnalyticsHosts = "profile.example,www.profile.example",
        });

        foreach (string file in Pages)
        {
            string page = File.ReadAllText(Path.Combine(_wwwroot, file));
            Assert.Contains("data-measurement-id=\"G-EXAMPLE123\" data-analytics-hosts=\"profile.example,www.profile.example\"", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Only_the_home_page_names_its_canonical_address()
    {
        WriteHome(profile: SiteProfile.None with { CanonicalOrigin = "https://site.example" });

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""
                    <meta property="og:url" content="https://site.example/" />
                    <link rel="canonical" href="https://site.example/" />
                </head>
            """, index, StringComparison.Ordinal);
        Assert.DoesNotContain("canonical", File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Storyblok_origin_is_used_by_metadata_crawlers_and_the_client()
    {
        const string Origin = "https://profile.example";
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata);
        File.WriteAllText(Path.Combine(_wwwroot, "robots.txt.br"), "stale");

        WriteHome(profile: SiteProfile.None with { CanonicalOrigin = Origin });

        string home = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains($"""<link rel="canonical" href="{Origin}/" />""", home, StringComparison.Ordinal);
        foreach (string page in Pages)
        {
            Assert.Contains($"""<meta property="og:url" content="{Origin}/" />""", File.ReadAllText(Path.Combine(_wwwroot, page)), StringComparison.Ordinal);
        }

        Assert.Equal($"{Origin}/", (string?)StructuredData(home)["url"]);
        Assert.Equal($"{Origin}/", (string?)StructuredData(home)["mainEntity"]!["url"]);
        Assert.Contains($"Sitemap: {Origin}/sitemap.xml", File.ReadAllText(Path.Combine(_wwwroot, "robots.txt")), StringComparison.Ordinal);
        Assert.Contains($"<loc>{Origin}/</loc>", File.ReadAllText(Path.Combine(_wwwroot, "sitemap.xml")), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_wwwroot, "robots.txt.br")));
    }

    [Fact]
    public void Only_the_shell_asks_for_JavaScript()
    {
        string page = Index.Replace("<body>", "<body>\n<noscript>\n<p>This page needs JavaScript</p>\n</noscript>", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), page);

        WriteHome();

        Assert.DoesNotContain("noscript", File.ReadAllText(Path.Combine(_wwwroot, "index.html")), StringComparison.Ordinal);
        Assert.Equal(page, File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")));
    }

    [Fact]
    public void Only_the_shell_connects_to_the_api_at_load()
    {
        const string Deferred = """<link rel="preconnect" href="https://api.storyblok.com" crossorigin data-preconnect-on-start />""";
        const string Other = """<link rel="preconnect" href="https://a.storyblok.com" />""";
        string page = Index.Replace("</head>", $"{Deferred}\n{Other}\n</head>", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), page);

        WriteHome();

        string home = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""<link href="https://api.storyblok.com" crossorigin data-preconnect-on-start />""", home, StringComparison.Ordinal);
        Assert.Contains(Other, home, StringComparison.Ordinal);
        Assert.Contains(Deferred, File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_without_a_head_is_left_alone()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), "<body><!--prerender--><!--/prerender--></body>");

        Assert.Throws<InvalidOperationException>(() => WriteHome());
        Assert.False(File.Exists(Path.Combine(_wwwroot, "app-shell.html")));
    }

    [Fact]
    public void Every_other_route_falls_back_to_the_shell()
    {
        WriteHome();

        JsonNode config = JsonNode.Parse(File.ReadAllText(Path.Combine(_wwwroot, "staticwebapp.config.json")))!;
        Assert.Equal("/app-shell.html", (string?)config["routes"]![0]!["rewrite"]);
        Assert.Equal("/app-shell.html", (string?)config["responseOverrides"]!["404"]!["rewrite"]);
        Assert.Equal(404, (int?)config["responseOverrides"]!["404"]!["statusCode"]);
        Assert.Equal("default-src 'self'; script-src 'self' 'sha256-abc+def='", (string?)config["globalHeaders"]!["Content-Security-Policy"]);
    }

    [Fact]
    public void Both_pages_get_the_photo_and_profiles_from_Storyblok()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata);
        SiteProfile profile = new("https://a.storyblok.com/f/1/600x900/new/me.jpg/m/600x600/smart", ["https://github.com/Layfully", "https://example.com/?a=1&b=<2>"]);

        WriteHome(profile: profile);

        foreach (string page in Pages.Select(page => File.ReadAllText(Path.Combine(_wwwroot, page))))
        {
            Assert.Contains("""<meta property="og:image" content="https://a.storyblok.com/f/1/600x900/new/me.jpg/m/600x600/smart" />""", page, StringComparison.Ordinal);
            Assert.Contains("""<meta property="og:image:width" content="600" />""", page, StringComparison.Ordinal);

            JsonNode person = StructuredData(page)["mainEntity"]!;
            Assert.Equal("Adrian Gaborek", (string?)person["name"]);
            Assert.Equal(profile.Image, (string?)person["image"]);
            Assert.Equal(profile.SameAs, person["sameAs"]!.AsArray().Select(address => (string?)address));
            // Nothing from Storyblok can end the script element early.
            Assert.DoesNotContain("<2>", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void What_Storyblok_doesnt_say_stays_as_written()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata);

        WriteHome();

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""<meta property="og:image" content="https://example.com/old.jpg" />""", index, StringComparison.Ordinal);
        Assert.Equal(["https://old.example.com"], StructuredData(index)["mainEntity"]!["sameAs"]!.AsArray().Select(address => (string?)address));
    }

    [Fact]
    public void Profile_identity_replaces_the_template_in_both_pages()
    {
        string template = IndexWithMetadata
            .Replace("<head>", "<head><title>Default</title><meta name=\"description\" content=\"Default\" />", StringComparison.Ordinal)
            .Replace("""<meta property="og:image" content="https://example.com/old.jpg" />""", string.Empty, StringComparison.Ordinal)
            .Replace("""<meta property="og:image:width" content="600" />""", string.Empty, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), template);

        WriteHome(profile: new SiteProfile("https://example.com/profile.jpg", [])
        {
            Name = "Example & Co",
            JobTitle = "Designer",
            Title = "Example & Co - Designer",
            Description = "A \"new\" profile",
        });

        foreach (string file in Pages)
        {
            string page = File.ReadAllText(Path.Combine(_wwwroot, file));
            Assert.Contains("<title>Example &amp; Co - Designer</title>", page, StringComparison.Ordinal);
            Assert.Contains("content=\"A &quot;new&quot; profile\"", page, StringComparison.Ordinal);
            Assert.Contains("""<meta property="og:image" content="https://example.com/profile.jpg" />""", page, StringComparison.Ordinal);
            JsonNode person = StructuredData(page)["mainEntity"]!;
            Assert.Equal("Example & Co", (string?)person["name"]);
            Assert.Equal("Designer", (string?)person["jobTitle"]);
        }
    }

    private static readonly string[] Pages = ["index.html", "app-shell.html"];

    [Theory]
    [InlineData(null, "icons/icon-maskable-512.png")]
    [InlineData("https://a.storyblok.com/f/1/mask.png", "https://a.storyblok.com/f/1/mask.png/m/512x512/filters:format(png)")]
    public void Install_branding_updates_the_manifest_and_touch_icon(string? maskable, string expectedMaskable)
    {
        const string Icon = "https://a.storyblok.com/f/1/icon.png";
        string path = Path.Combine(_wwwroot, "site.webmanifest");
        File.WriteAllText(path, """
            { "id": "/", "name": "Links", "icons": [
              { "src": "icons/icon-192.png", "sizes": "192x192", "type": "image/png" },
              { "src": "icons/icon-512.png", "sizes": "512x512", "type": "image/png" },
              { "src": "icons/icon-maskable-512.png", "purpose": "maskable" }
            ] }
            """);
        File.WriteAllText(path + ".br", "old");
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata.Replace("<head>",
            """<head><link rel="apple-touch-icon" href="icons/icon-180.png" />""", StringComparison.Ordinal));

        WriteHome(profile: SiteProfile.None with { AppName = "Example & Co", AppIcon = Icon, AppMaskableIcon = maskable });

        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal("Example & Co", (string?)manifest["name"]);
        Assert.Equal("/", (string?)manifest["id"]);
        Assert.Equal($"{Icon}/m/192x192/filters:format(png)", (string?)manifest["icons"]![0]!["src"]);
        Assert.Equal($"{Icon}/m/512x512/filters:format(png)", (string?)manifest["icons"]![1]!["src"]);
        Assert.Equal(expectedMaskable, (string?)manifest["icons"]![2]!["src"]);
        Assert.False(File.Exists(path + ".br"));
        foreach (string page in Pages)
        {
            Assert.Contains($"href=\"{Icon}/m/180x180/filters:format(png)\"", File.ReadAllText(Path.Combine(_wwwroot, page)), StringComparison.Ordinal);
        }
    }

    private const string IndexWithMetadata = """
        <html>
            <head>
                <meta property="og:image" content="https://example.com/old.jpg" />
                <meta property="og:image:width" content="600" />
                <script type="application/ld+json">
                    { "@context": "https://schema.org", "@type": "ProfilePage",
                      "mainEntity": { "@type": "Person", "name": "Adrian Gaborek", "image": "https://example.com/old.jpg", "sameAs": ["https://old.example.com"] } }
                </script>
            </head>
        <body>
        <div id="app"><!--prerender--><p>Loading</p><!--/prerender--></div>
        </body></html>
        """;

    private static JsonNode StructuredData(string page)
    {
        const string Start = """<script type="application/ld+json">""";
        int start = page.IndexOf(Start, StringComparison.Ordinal) + Start.Length;
        return JsonNode.Parse(page[start..page.IndexOf("</script>", start, StringComparison.Ordinal)])!;
    }

    [Fact]
    public void Compressed_copies_of_the_old_page_are_removed()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html.br"), "old");
        File.WriteAllText(Path.Combine(_wwwroot, "index.html.gz"), "old");

        WriteHome();

        Assert.False(File.Exists(Path.Combine(_wwwroot, "index.html.br")));
        Assert.False(File.Exists(Path.Combine(_wwwroot, "index.html.gz")));
    }
}
