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
        IEnumerable<string> used = Directory.EnumerateFiles(client, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => FontAwesomeSubset.IconsIn(WithoutComments(File.ReadAllText(file))));

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

    [Fact]
    public void Without_pyftsubset_nothing_changes()
    {
        string wwwroot = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            const string Page = """<link rel="preload" as="style" href="lib/fontawesome-7.3.1/css/all.min.css" />""";
            Directory.CreateDirectory(Path.Combine(wwwroot, "lib", "fontawesome-7.3.1", "css"));
            File.WriteAllText(Path.Combine(wwwroot, "lib", "fontawesome-7.3.1", "css", "all.min.css"), """.fa-github{--fa:"\f09b"}""");
            File.WriteAllText(Path.Combine(wwwroot, "index.html"), Page);

            bool applied = new FontAwesomeSubset("pyftsubset-that-isnt-installed").Apply(wwwroot, new HashSet<string> { "fa-github" });

            Assert.False(applied);
            Assert.Equal(Page, File.ReadAllText(Path.Combine(wwwroot, "index.html")));
            Assert.Single(Directory.GetDirectories(Path.Combine(wwwroot, "lib")));
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
