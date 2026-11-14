using Prerender;
using System.Text.Json.Nodes;

namespace LinkInBio.Tests.Prerender;

/// <summary>How the prerendered home page is written into a published site.</summary>
public sealed class PublishedSiteTests : IDisposable
{
    private const string Index = """
        <html><body>
        <div id="app"><!--prerender--><p>Loading</p><!--/prerender--></div>
        <script src="_framework/blazor.webassembly.js"></script>
        </body></html>
        """;

    private const string Config = """
        {
          "navigationFallback": {
            "rewrite": "/index.html",
            "exclude": ["/_framework/*"]
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
        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", """{"home":{}}""");

        string index = File.ReadAllText(Path.Combine(_wwwroot, "index.html"));
        Assert.Contains("""<div id="app"><main>Links</main></div>""", index, StringComparison.Ordinal);
        Assert.Contains("""<script type="application/json" id="storyblok-prerendered">{"home":{}}</script>""", index, StringComparison.Ordinal);
        Assert.DoesNotContain("Loading", index, StringComparison.Ordinal);
        Assert.Equal(Index, File.ReadAllText(Path.Combine(_wwwroot, "app-shell.html")));
    }

    [Fact]
    public void Every_other_route_falls_back_to_the_shell()
    {
        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}");

        JsonNode config = JsonNode.Parse(File.ReadAllText(Path.Combine(_wwwroot, "staticwebapp.config.json")))!;
        Assert.Equal("/app-shell.html", (string?)config["navigationFallback"]!["rewrite"]);
        Assert.Equal("default-src 'self'; script-src 'self' 'sha256-abc+def='", (string?)config["globalHeaders"]!["Content-Security-Policy"]);
    }

    [Fact]
    public void Compressed_copies_of_the_old_page_are_removed()
    {
        File.WriteAllText(Path.Combine(_wwwroot, "index.html.br"), "old");
        File.WriteAllText(Path.Combine(_wwwroot, "index.html.gz"), "old");

        new PublishedSite(_wwwroot).WriteHome("<main>Links</main>", "{}");

        Assert.False(File.Exists(Path.Combine(_wwwroot, "index.html.br")));
        Assert.False(File.Exists(Path.Combine(_wwwroot, "index.html.gz")));
    }
}
