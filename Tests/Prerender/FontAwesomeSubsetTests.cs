using Prerender;
using System.Text.RegularExpressions;

namespace LinkInBio.Tests.Prerender;

/// <summary>Which Font Awesome icons the published site keeps.</summary>
public sealed partial class FontAwesomeSubsetTests
{
    [Fact]
    public void IconsIn_ShouldFindEveryIconAComponentUses_WhenScanningTheRazorFiles()
    {
        // Arrange
        string client = Path.Combine(RepositoryRoot(), "Client");

        // Act
        HashSet<string> used = Directory.EnumerateFiles(client, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => FontAwesomeSubset.IconsIn(WithoutComments(File.ReadAllText(file))))
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        used.ShouldContain("fa-cloud-bolt"); // the scan found the components' icons at all
        used.ShouldAllBe(icon => FontAwesomeSubset.ComponentIcons.Contains(icon));
    }

    [Fact]
    public void IconsIn_ShouldReturnTheIconClasses_WhenGivenTheRenderedPage()
    {
        // Arrange
        const string Page = """<i class="fab fa-github"></i><i class="fa-solid fa-arrow-right"></i>""";

        // Act
        IReadOnlySet<string> icons = FontAwesomeSubset.IconsIn(Page);

        // Assert
        icons.Order(StringComparer.Ordinal).ShouldBe(["fa-arrow-right", "fa-github"]);
    }

    [Fact]
    public void Filter_ShouldDropUnusedIconRulesAndKeepSharedOnes_WhenOnlySomeIconsAreUsed()
    {
        // Arrange
        const string Css = """.fa{display:inline-block}.fa-github{--fa:"\f09b"}.fa-gitlab{--fa:"\f296"}.fa-a,.fa-b{--fa:"\f001"}.fa-0{--fa:"\30 "}.fa-usd{--fa:"\$"}""";

        // Act
        (string css, IReadOnlySet<int> codepoints) = FontAwesomeSubset.Filter(Css, new HashSet<string> { "fa-github", "fa-b", "fa-0", "fa-usd" });

        // Assert
        css.ShouldBe(""".fa{display:inline-block}.fa-github{--fa:"\f09b"}.fa-a,.fa-b{--fa:"\f001"}.fa-0{--fa:"\30 "}.fa-usd{--fa:"\$"}""");
        codepoints.Order().ShouldBe([0x24, 0x30, 0xf001, 0xf09b]);
    }

    [Theory]
    [InlineData("7.3.1")]
    [InlineData("8.0.2")]
    public void Apply_ShouldLeaveTheLinkedVersionAsIs_WhenPyftsubsetIsNotInstalled(string version)
    {
        // Arrange
        string wwwroot = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            string folder = $"fontawesome-{version}";
            string page = $"""<link rel="preload" as="style" href="lib/{folder}/css/all.min.css" />""";
            DirectoryInfo cssDirectory = Directory.CreateDirectory(Path.Combine(wwwroot, "lib", folder, "css"));
            File.WriteAllText(Path.Combine(cssDirectory.FullName, "all.min.css"), """.fa-github{--fa:"\f09b"}""");
            File.WriteAllText(Path.Combine(wwwroot, "index.html"), page);
            FontAwesomeSubset sut = new("pyftsubset-that-isnt-installed");

            // Act
            bool applied = sut.Apply(wwwroot, new HashSet<string> { "fa-github" });

            // Assert
            applied.ShouldBeFalse();
            File.ReadAllText(Path.Combine(wwwroot, "index.html")).ShouldBe(page);
            Directory.GetDirectories(Path.Combine(wwwroot, "lib")).ShouldHaveSingleItem();
        }
        finally
        {
            Directory.Delete(wwwroot, recursive: true);
        }
    }

    [Fact]
    public void Apply_ShouldThrow_WhenThePageHasNoStylesheetLink()
    {
        // Arrange
        string wwwroot = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(wwwroot, "index.html"), "<html></html>");
            FontAwesomeSubset sut = new();

            // Act
            InvalidOperationException error = Should.Throw<InvalidOperationException>(() => sut.Apply(wwwroot, new HashSet<string>()));

            // Assert
            error.Message.ShouldContain("lib/fontawesome-<version>/css/all.min.css", Case.Sensitive);
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
