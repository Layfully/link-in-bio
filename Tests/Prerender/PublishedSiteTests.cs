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
    }

    public void Dispose() => Directory.Delete(_wwwroot, recursive: true);

    [Fact]
    public void The_home_page_is_written_between_the_markers_and_the_shell_is_kept()
    {
        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", """{"home":{}}""", SiteProfile.None);

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""<div id="app"><main>Links</main></div>""", index, StringComparison.Ordinal);
        Assert.Contains("""<script type="application/json" id="storyblok-prerendered">{"home":{}}</script>""", index, StringComparison.Ordinal);
        Assert.DoesNotContain("Loading", index, StringComparison.Ordinal);
        Assert.Equal(Index, File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")));
    }

    [Fact]
    public void Only_the_home_page_names_its_canonical_address()
    {
        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}", SiteProfile.None);

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""
                    <title>Links</title>
                    <link rel="canonical" href="https://adriangaborek.dev/" />
                </head>
            """, index, StringComparison.Ordinal);
        Assert.DoesNotContain("canonical", File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_without_a_head_is_left_alone()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html"), "<body><!--prerender--><!--/prerender--></body>");

        Assert.Throws<InvalidOperationException>(() => new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}", SiteProfile.None));
        Assert.False(File.Exists(Path.Combine(_wwwroot, "app-shell.html")));
    }

    [Fact]
    public void Every_other_route_falls_back_to_the_shell()
    {
        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}", SiteProfile.None);

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

        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}", profile);

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

        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}", SiteProfile.None);

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""<meta property="og:image" content="https://example.com/old.jpg" />""", index, StringComparison.Ordinal);
        Assert.Equal(["https://old.example.com"], StructuredData(index)["mainEntity"]!["sameAs"]!.AsArray().Select(address => (string?)address));
    }

    private static readonly string[] Pages = ["index.html", "app-shell.html"];

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

        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}", SiteProfile.None);

        Assert.False(File.Exists(Path.Combine(_wwwroot, "index.html.br")));
        Assert.False(File.Exists(Path.Combine(_wwwroot, "index.html.gz")));
    }
}
