using Prerender;
using System.Text.RegularExpressions;

namespace LinkInBio.Tests.Prerender;

/// <summary>Which Font Awesome icons the published site keeps.</summary>
public sealed partial class FontAwesomeSubsetTests
{
    [Fact]
    public void Every_icon_a_component_uses_is_kept()
    {
        string client = Path.Combine(RepositoryRoot(), "Client");
        HashSet<string> used = Directory.EnumerateFiles(client, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => FontAwesomeSubset.IconsIn(WithoutComments(File.ReadAllText(file))))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("fa-cloud-bolt", used); // the scan found the components' icons at all
        Assert.All(used, icon => Assert.Contains(icon, FontAwesomeSubset.ComponentIcons));
    }

    [Fact]
    public void Icons_are_read_from_the_rendered_page()
    {
        IReadOnlySet<string> icons = FontAwesomeSubset.IconsIn("""<i class="fab fa-github"></i><i class="fa-solid fa-arrow-right"></i>""");

        Assert.Equal(["fa-arrow-right", "fa-github"], icons.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Unused_icon_rules_are_dropped_and_shared_rules_kept()
    {
        const string Css = """.fa{display:inline-block}.fa-github{--fa:"\f09b"}.fa-gitlab{--fa:"\f296"}.fa-a,.fa-b{--fa:"\f001"}.fa-0{--fa:"\30 "}.fa-usd{--fa:"\$"}""";

        (string css, IReadOnlySet<int> codepoints) = FontAwesomeSubset.Filter(Css, new HashSet<string> { "fa-github", "fa-b", "fa-0", "fa-usd" });

        Assert.Equal(""".fa{display:inline-block}.fa-github{--fa:"\f09b"}.fa-a,.fa-b{--fa:"\f001"}.fa-0{--fa:"\30 "}.fa-usd{--fa:"\$"}""", css);
        Assert.Equal([0x24, 0x30, 0xf001, 0xf09b], codepoints.Order());
    }

    [Theory]
    [InlineData("7.3.1")]
    [InlineData("8.0.2")]
    public void Without_pyftsubset_the_linked_version_is_left_as_is(string version)
    {
        string wwwroot = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            string folder = $"fontawesome-{version}";
            string page = $"""<link rel="preload" as="style" href="lib/{folder}/css/all.min.css" />""";
            DirectoryInfo cssDirectory = Directory.CreateDirectory(Path.Combine(wwwroot, "lib", folder, "css"));
            File.WriteAllText(Path.Combine(cssDirectory.FullName, "all.min.css"), """.fa-github{--fa:"\f09b"}""");
            File.WriteAllText(Path.Combine(wwwroot, "index.html"), page);

            bool applied = new FontAwesomeSubset("pyftsubset-that-isnt-installed").Apply(wwwroot, new HashSet<string> { "fa-github" });

            Assert.False(applied);
            Assert.Equal(page, File.ReadAllText(Path.Combine(wwwroot, "index.html")));
            Assert.Single(Directory.GetDirectories(Path.Combine(wwwroot, "lib")));
        }
        finally
        {
            Directory.Delete(wwwroot, recursive: true);
        }
    }

    [Fact]
    public void A_page_without_the_stylesheet_link_is_reported()
    {
        string wwwroot = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(wwwroot, "index.html"), "<html></html>");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new FontAwesomeSubset().Apply(wwwroot, new HashSet<string>()));
            Assert.Contains("lib/fontawesome-<version>/css/all.min.css", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(wwwroot, recursive: true);
        }
    }

    // Comments may mention icons as examples (/// <c>fab fa-github</c>); only markup and code count.
    private static string WithoutComments(string razor) => RazorComment().Replace(DocComment().Replace(razor, string.Empty), string.Empty);

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "link-in-bio.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (link-in-bio.slnx) wasn't found.");
    }

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"^\s*///.*$", RegexOptions.Multiline)]
    private static partial Regex DocComment();
}
