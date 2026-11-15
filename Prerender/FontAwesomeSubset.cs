using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Prerender;

/// <summary>
/// Cuts Font Awesome in a published site down to the icons the site uses: its stylesheet has a rule for each of about
/// 2,000 icons and its fonts a glyph for each, while the site shows about ten. The cut-down copy goes to a new folder
/// named after its content, because browsers keep everything under <c>lib/</c> for a year (README, Caching).
/// </summary>
/// <param name="pyftsubset">The fonttools command that subsets the fonts; the deploy installs it.</param>
public sealed partial class FontAwesomeSubset(string pyftsubset = "pyftsubset")
{
    private const string Stylesheet = "css/all.min.css";

    // The fonts with the icons the site uses. The others (regular, v4 compatibility) are small and copied as they are.
    private static readonly string[] SubsetFonts = ["fa-brands-400.woff2", "fa-solid-900.woff2"];

    // Classes that choose a style or size rather than an icon.
    private static readonly HashSet<string> StyleClasses = new(StringComparer.Ordinal) { "fa-solid", "fa-regular", "fa-brands", "fa-classic" };

    /// <summary>
    /// Icons the components show whatever Storyblok says, including ones the home page doesn't render (error and
    /// not-found notices). A test checks that every icon in a .razor file is listed here.
    /// </summary>
    public static IReadOnlySet<string> ComponentIcons { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "fa-arrow-right",
        "fa-circle-question",
        "fa-cloud-bolt",
        "fa-file-circle-exclamation",
        "fa-link-slash",
        "fa-triangle-exclamation",
    };

    /// <summary>The Font Awesome icon classes in a piece of HTML or Razor, without the style classes.</summary>
    public static IReadOnlySet<string> IconsIn(string html) =>
        IconClass().Matches(html).Select(match => match.Value).Where(icon => !StyleClasses.Contains(icon)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The stylesheet without the rules of icons that aren't used (<c>.fa-gitlab{--fa:"\f296"}</c>); every other rule
    /// stays. A rule for several names of one icon stays when any of them is used. Also returns the characters the kept
    /// icons draw, which the fonts must keep.
    /// </summary>
    public static (string Css, IReadOnlySet<int> Codepoints) Filter(string css, IReadOnlySet<string> icons)
    {
        HashSet<int> codepoints = [];
        string filtered = IconRule().Replace(css, rule =>
        {
            if (!rule.Groups["selectors"].Value.Split(',').Any(selector => icons.Contains(selector.TrimStart('.'))))
            {
                return string.Empty;
            }

            codepoints.UnionWith(Codepoints(rule.Groups["content"].Value));
            return rule.Value;
        });

        return (filtered, codepoints);
    }

    /// <summary>
    /// Writes the cut-down copy and points <c>index.html</c> and <c>app-shell.html</c> at it. False, with nothing
    /// changed, when <c>pyftsubset</c> isn't installed.
    /// </summary>
    public bool Apply(string wwwroot, IReadOnlySet<string> icons)
    {
        // The version folder index.html links (lib/fontawesome-7.3.1), so updating Font Awesome needs no change here.
        string folder = StylesheetLink().Match(File.ReadAllText(Path.Combine(wwwroot, "index.html"))) is { Success: true } link
            ? link.Groups["folder"].Value
            : throw new InvalidOperationException($"index.html links no lib/fontawesome-<version>/{Stylesheet}.");
        string source = Path.Combine(wwwroot, "lib", folder);
        (string css, IReadOnlySet<int> codepoints) = Filter(File.ReadAllText(Path.Combine(source, Stylesheet)), icons);

        string work = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            foreach (string font in SubsetFonts)
            {
                if (!TrySubset(Path.Combine(source, "webfonts", font), codepoints, Path.Combine(work, font)))
                {
                    return false;
                }
            }

            string target = Path.Combine(wwwroot, "lib", $"{folder}-{ContentHash(css, work)}");
            Directory.CreateDirectory(Path.Combine(target, "css"));
            Directory.CreateDirectory(Path.Combine(target, "webfonts"));
            File.WriteAllText(Path.Combine(target, Stylesheet), css);
            foreach (string font in Directory.EnumerateFiles(Path.Combine(source, "webfonts")))
            {
                string name = Path.GetFileName(font);
                File.Copy(SubsetFonts.Contains(name) ? Path.Combine(work, name) : font, Path.Combine(target, "webfonts", name), overwrite: true);
            }

            // The fonts' license (SIL OFL) asks for it to travel with them.
            File.Copy(Path.Combine(source, "LICENSE.txt"), Path.Combine(target, "LICENSE.txt"), overwrite: true);

            string from = $"lib/{folder}/{Stylesheet}";
            string to = $"lib/{Path.GetFileName(target)}/{Stylesheet}";
            foreach (string page in new[] { "index.html", PublishedSite.ShellFile }.Select(page => Path.Combine(wwwroot, page)).Where(File.Exists))
            {
                File.WriteAllText(page, File.ReadAllText(page).Replace(from, to, StringComparison.Ordinal));
            }

            return true;
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private bool TrySubset(string font, IReadOnlySet<int> codepoints, string output)
    {
        ProcessStartInfo start = new(pyftsubset) { UseShellExecute = false, RedirectStandardError = true };
        start.ArgumentList.Add(font);
        start.ArgumentList.Add("--unicodes=" + string.Join(',', codepoints.Order().Select(codepoint => $"U+{codepoint:X4}")));
        start.ArgumentList.Add("--flavor=woff2");
        start.ArgumentList.Add("--output-file=" + output);

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException($"{pyftsubset} didn't start.");
        }
        catch (Win32Exception)
        {
            return false; // not installed
        }

        using (process)
        {
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0
                ? true
                : throw new InvalidOperationException($"{pyftsubset} failed for {Path.GetFileName(font)}: {errors}");
        }
    }

    // The folder name changes whenever the stylesheet or a font does, so a browser never keeps an outdated copy.
    private static string ContentHash(string css, string fonts)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(css));
        foreach (string font in SubsetFonts)
        {
            hash.AppendData(File.ReadAllBytes(Path.Combine(fonts, font)));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset())[..8];
    }

    // A CSS string's characters: "\f09b" (hex, optionally ended by a space), "\$" (an escaped character) or plain text.
    private static IEnumerable<int> Codepoints(string content)
    {
        foreach (Match escape in CssCharacter().Matches(content))
        {
            if (escape.Groups["hex"].Success)
            {
                yield return int.Parse(escape.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            else
            {
                yield return char.ConvertToUtf32(escape.Groups["char"].Value, 0);
            }
        }
    }

    // A version folder as published, never a cut-down copy (fontawesome-7.3.1-1a2b3c4d).
    [GeneratedRegex(@"lib/(?<folder>fontawesome-[0-9][0-9.]*)/css/all\.min\.css")]
    private static partial Regex StylesheetLink();

    [GeneratedRegex(@"\bfa-[a-z0-9]+(?:-[a-z0-9]+)*")]
    private static partial Regex IconClass();

    [GeneratedRegex(@"(?<selectors>\.fa-[a-z0-9-]+(?:,\.fa-[a-z0-9-]+)*)\{--fa:""(?<content>(?:\\[0-9a-fA-F]{1,6} ?|\\.|[^""\\])+)""\}")]
    private static partial Regex IconRule();

    [GeneratedRegex(@"\\(?<hex>[0-9a-fA-F]{1,6}) ?|\\(?<char>.)|(?<char>.)", RegexOptions.Singleline)]
    private static partial Regex CssCharacter();
}
