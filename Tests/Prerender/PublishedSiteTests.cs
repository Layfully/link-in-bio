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
    private readonly PublishedSite _sut;

    public PublishedSiteTests()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), Index);
        File.WriteAllText(Path.Combine(_wwwroot, "staticwebapp.config.json"), Config);
        File.WriteAllText(Path.Combine(_wwwroot, "robots.txt"), "User-agent: *\nAllow: /\n");
        _sut = new PublishedSite(_wwwroot);
    }

    public void Dispose() => Directory.Delete(_wwwroot, recursive: true);

    private void WriteHome(string stories = "{}", SiteProfile? profile = null) =>
        _sut.WriteHome("<main>Links</main>", stories, profile ?? SiteProfile.None);

    [Fact]
    public void WriteHome_ShouldWriteBetweenTheMarkersAndKeepTheShell_WhenTheProfileIsEmpty()
    {
        // Act
        WriteHome("""{"home":{}}""");

        // Assert
        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        index.ShouldContain("""<div id="app"><main>Links</main></div>""", Case.Sensitive);
        index.ShouldContain("""<script type="application/json" id="storyblok-prerendered">{"home":{}}</script>""", Case.Sensitive);
        index.ShouldNotContain("Loading", Case.Sensitive);
        File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")).ShouldBe(Index);
        index.ShouldNotContain("rel=\"canonical\"", Case.Sensitive);
        index.ShouldNotContain("data-measurement-id", Case.Sensitive);
        File.Exists(Path.Combine(_wwwroot, "sitemap.xml")).ShouldBeFalse();
        File.ReadAllText(Path.Combine(_wwwroot, "robots.txt")).ShouldNotContain("Sitemap:", Case.Sensitive);
    }

    [Fact]
    public void WriteHome_ShouldAddAnalyticsSettingsToBothPages_WhenTheProfileHasThem()
    {
        // Act
        WriteHome(profile: SiteProfile.None with
        {
            AnalyticsMeasurementId = "G-EXAMPLE123",
            AnalyticsHosts = "profile.example,www.profile.example",
        });

        // Assert
        foreach (string file in Pages)
        {
            string page = File.ReadAllText(Path.Combine(_wwwroot, file));
            page.ShouldContain("data-measurement-id=\"G-EXAMPLE123\" data-analytics-hosts=\"profile.example,www.profile.example\"", Case.Sensitive);
        }
    }

    [Fact]
    public void WriteHome_ShouldNameTheCanonicalAddressOnlyOnTheHomePage_WhenTheProfileHasAnOrigin()
    {
        // Act
        WriteHome(profile: SiteProfile.None with { CanonicalOrigin = "https://site.example" });

        // Assert
        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        index.ShouldContain("""
                    <meta property="og:url" content="https://site.example/" />
                    <link rel="canonical" href="https://site.example/" />
                </head>
            """, Case.Sensitive);
        File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")).ShouldNotContain("canonical", Case.Sensitive);
    }

    [Fact]
    public void WriteHome_ShouldUseTheStoryblokOriginForMetadataCrawlersAndTheClient_WhenTheProfileHasAnOrigin()
    {
        // Arrange
        const string Origin = "https://profile.example";
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata);
        File.WriteAllText(Path.Combine(_wwwroot, "robots.txt.br"), "stale");

        // Act
        WriteHome(profile: SiteProfile.None with { CanonicalOrigin = Origin });

        // Assert
        string home = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        home.ShouldContain($"""<link rel="canonical" href="{Origin}/" />""", Case.Sensitive);
        foreach (string page in Pages)
        {
            File.ReadAllText(Path.Combine(_wwwroot, page)).ShouldContain($"""<meta property="og:url" content="{Origin}/" />""", Case.Sensitive);
        }

        ((string?)StructuredData(home)["url"]).ShouldBe($"{Origin}/");
        ((string?)StructuredData(home)["mainEntity"]!["url"]).ShouldBe($"{Origin}/");
        File.ReadAllText(Path.Combine(_wwwroot, "robots.txt")).ShouldContain($"Sitemap: {Origin}/sitemap.xml", Case.Sensitive);
        File.ReadAllText(Path.Combine(_wwwroot, "sitemap.xml")).ShouldContain($"<loc>{Origin}/</loc>", Case.Sensitive);
        File.Exists(Path.Combine(_wwwroot, "robots.txt.br")).ShouldBeFalse();
    }

    [Fact]
    public void WriteHome_ShouldKeepTheJavaScriptNoticeOnlyInTheShell_WhenTheTemplateHasOne()
    {
        // Arrange
        string page = Index.Replace("<body>", "<body>\n<noscript>\n<p>This page needs JavaScript</p>\n</noscript>", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), page);

        // Act
        WriteHome();

        // Assert
        File.ReadAllText(Path.Combine(_wwwroot, "index.html")).ShouldNotContain("noscript", Case.Sensitive);
        File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")).ShouldBe(page);
    }

    [Fact]
    public void WriteHome_ShouldConnectToTheApiAtLoadOnlyInTheShell_WhenTheTemplateDefersThePreconnect()
    {
        // Arrange
        const string Deferred = """<link rel="preconnect" href="https://api.storyblok.com" crossorigin data-preconnect-on-start />""";
        const string Other = """<link rel="preconnect" href="https://a.storyblok.com" />""";
        string page = Index.Replace("</head>", $"{Deferred}\n{Other}\n</head>", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), page);

        // Act
        WriteHome();

        // Assert
        string home = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        home.ShouldContain("""<link href="https://api.storyblok.com" crossorigin data-preconnect-on-start />""", Case.Sensitive);
        home.ShouldContain(Other, Case.Sensitive);
        File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")).ShouldContain(Deferred, Case.Sensitive);
    }

    [Fact]
    public void WriteHome_ShouldThrowAndLeaveThePageAlone_WhenThePageHasNoHead()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), "<body><!--prerender--><!--/prerender--></body>");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => WriteHome());
        File.Exists(Path.Combine(_wwwroot, "app-shell.html")).ShouldBeFalse();
    }

    [Fact]
    public void WriteHome_ShouldFallBackToTheShellForEveryOtherRoute_WhenTheConfigRewritesToTheIndex()
    {
        // Act
        WriteHome();

        // Assert
        JsonNode config = JsonNode.Parse(File.ReadAllText(Path.Combine(_wwwroot, "staticwebapp.config.json")))!;
        ((string?)config["routes"]![0]!["rewrite"]).ShouldBe("/app-shell.html");
        ((string?)config["responseOverrides"]!["404"]!["rewrite"]).ShouldBe("/app-shell.html");
        ((int?)config["responseOverrides"]!["404"]!["statusCode"]).ShouldBe(404);
        ((string?)config["globalHeaders"]!["Content-Security-Policy"]).ShouldBe("default-src 'self'; script-src 'self' 'sha256-abc+def='");
    }

    [Fact]
    public void WriteHome_ShouldAddThePhotoAndProfilesToBothPages_WhenTheProfileHasThem()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata);
        SiteProfile profile = new("https://a.storyblok.com/f/1/600x900/new/me.jpg/m/600x600/smart", ["https://github.com/Layfully", "https://example.com/?a=1&b=<2>"]);

        // Act
        WriteHome(profile: profile);

        // Assert
        foreach (string page in Pages.Select(page => File.ReadAllText(Path.Combine(_wwwroot, page))))
        {
            page.ShouldContain("""<meta property="og:image" content="https://a.storyblok.com/f/1/600x900/new/me.jpg/m/600x600/smart" />""", Case.Sensitive);
            page.ShouldContain("""<meta property="og:image:width" content="600" />""", Case.Sensitive);

            JsonNode person = StructuredData(page)["mainEntity"]!;
            ((string?)person["name"]).ShouldBe("Adrian Gaborek");
            ((string?)person["image"]).ShouldBe(profile.Image);
            person["sameAs"]!.AsArray().Select(address => (string?)address).ShouldBe(profile.SameAs);
            // Nothing from Storyblok can end the script element early.
            page.ShouldNotContain("<2>", Case.Sensitive);
        }
    }

    [Fact]
    public void WriteHome_ShouldKeepTheTemplateAsWritten_WhenTheProfileSaysNothing()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), IndexWithMetadata);

        // Act
        WriteHome();

        // Assert
        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        index.ShouldContain("""<meta property="og:image" content="https://example.com/old.jpg" />""", Case.Sensitive);
        StructuredData(index)["mainEntity"]!["sameAs"]!.AsArray().Select(address => (string?)address).ShouldBe(["https://old.example.com"]);
    }

    [Fact]
    public void WriteHome_ShouldReplaceTheTemplateIdentityInBothPages_WhenTheProfileHasAName()
    {
        // Arrange
        string template = IndexWithMetadata
            .Replace("<head>", "<head><title>Default</title><meta name=\"description\" content=\"Default\" />", StringComparison.Ordinal)
            .Replace("""<meta property="og:image" content="https://example.com/old.jpg" />""", string.Empty, StringComparison.Ordinal)
            .Replace("""<meta property="og:image:width" content="600" />""", string.Empty, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), template);

        // Act
        WriteHome(profile: new SiteProfile("https://example.com/profile.jpg", [])
        {
            Name = "Example & Co",
            JobTitle = "Designer",
            Title = "Example & Co - Designer",
            Description = "A \"new\" profile",
        });

        // Assert
        foreach (string file in Pages)
        {
            string page = File.ReadAllText(Path.Combine(_wwwroot, file));
            page.ShouldContain("<title>Example &amp; Co - Designer</title>", Case.Sensitive);
            page.ShouldContain("content=\"A &quot;new&quot; profile\"", Case.Sensitive);
            page.ShouldContain("""<meta property="og:image" content="https://example.com/profile.jpg" />""", Case.Sensitive);
            JsonNode person = StructuredData(page)["mainEntity"]!;
            ((string?)person["name"]).ShouldBe("Example & Co");
            ((string?)person["jobTitle"]).ShouldBe("Designer");
        }
    }

    private static readonly string[] Pages = ["index.html", "app-shell.html"];

    [Theory]
    [InlineData(null, "icons/icon-maskable-512.png")]
    [InlineData("https://a.storyblok.com/f/1/mask.png", "https://a.storyblok.com/f/1/mask.png/m/512x512/filters:format(png)")]
    public void WriteHome_ShouldUpdateTheManifestAndTouchIcon_WhenTheProfileHasInstallBranding(string? maskable, string expectedMaskable)
    {
        // Arrange
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

        // Act
        WriteHome(profile: SiteProfile.None with { AppName = "Example & Co", AppIcon = Icon, AppMaskableIcon = maskable });

        // Assert
        JsonNode manifest = JsonNode.Parse(File.ReadAllText(path))!;
        ((string?)manifest["name"]).ShouldBe("Example & Co");
        ((string?)manifest["id"]).ShouldBe("/");
        ((string?)manifest["icons"]![0]!["src"]).ShouldBe($"{Icon}/m/192x192/filters:format(png)");
        ((string?)manifest["icons"]![1]!["src"]).ShouldBe($"{Icon}/m/512x512/filters:format(png)");
        ((string?)manifest["icons"]![2]!["src"]).ShouldBe(expectedMaskable);
        File.Exists(path + ".br").ShouldBeFalse();
        foreach (string page in Pages)
        {
            File.ReadAllText(Path.Combine(_wwwroot, page)).ShouldContain($"href=\"{Icon}/m/180x180/filters:format(png)\"", Case.Sensitive);
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
    public void WriteHome_ShouldRemoveCompressedCopiesOfTheOldPage_WhenTheyExist()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_wwwroot, "index.html.br"), "old");
        File.WriteAllText(Path.Combine(_wwwroot, "index.html.gz"), "old");

        // Act
        WriteHome();

        // Assert
        File.Exists(Path.Combine(_wwwroot, "index.html.br")).ShouldBeFalse();
        File.Exists(Path.Combine(_wwwroot, "index.html.gz")).ShouldBeFalse();
    }
}
