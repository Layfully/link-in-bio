using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Prerender;

/// <summary>Publishes the site's Font Awesome subset under a content hash for browser caching.</summary>
/// <param name="pyftsubset">Path or command name for the fontTools subsetter.</param>
public sealed partial class FontAwesomeSubset(string pyftsubset = "pyftsubset")
{
    private const string Stylesheet = "css/all.min.css";

    // Other font files are copied in full.
    private static readonly string[] SubsetFonts = ["fa-brands-400.woff2", "fa-solid-900.woff2"];

    /// <summary>Icons required by components, including error and not-found pages.</summary>
    public static IReadOnlySet<string> ComponentIcons { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "fa-arrow-right",
        "fa-circle-question",
        "fa-cloud-bolt",
        "fa-file-circle-exclamation",
        "fa-link-slash",
        "fa-triangle-exclamation",
    };

    /// <summary>Extracts icon classes from HTML or Razor markup.</summary>
    public static IReadOnlySet<string> IconsIn(string html)
    {
        HashSet<string> icons = new(StringComparer.Ordinal);
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> found = icons.GetAlternateLookup<ReadOnlySpan<char>>();
        foreach (ValueMatch match in IconClass().EnumerateMatches(html))
        {
            ReadOnlySpan<char> icon = html.AsSpan(match.Index, match.Length);
            if (icon is not ("fa-solid" or "fa-regular" or "fa-brands" or "fa-classic"))
            {
                found.Add(icon);
            }
        }

        return icons;
    }

    /// <summary>Removes unused icon rules and returns the codepoints to keep. Any matching alias keeps its rule.</summary>
    public static (string Css, IReadOnlySet<int> Codepoints) Filter(string css, IReadOnlySet<string> icons)
    {
        HashSet<int> codepoints = [];
        Func<ReadOnlySpan<char>, bool> containsIcon = IconLookup(icons);
        string filtered = IconRule().Replace(css, rule =>
        {
            if (!UsesIcon(rule.Groups["selectors"].ValueSpan, containsIcon))
            {
                return string.Empty;
            }

            AddCodepoints(rule.Groups["content"].Value, codepoints);
            return rule.Value;
        });

        return (filtered, codepoints);
    }

    private static Func<ReadOnlySpan<char>, bool> IconLookup(IReadOnlySet<string> icons)
    {
        if (icons is HashSet<string> set && set.TryGetAlternateLookup(out HashSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup))
        {
            return lookup.Contains;
        }

        return icon => icons.Contains(icon.ToString());
    }

    private static bool UsesIcon(ReadOnlySpan<char> selectors, Func<ReadOnlySpan<char>, bool> containsIcon)
    {
        foreach (Range range in selectors.Split(','))
        {
            ReadOnlySpan<char> selector = selectors[range];
            if (containsIcon(selector[1..])) // Skip the leading dot.
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Publishes the subset and updates the page links. Returns false if the subsetter cannot start.</summary>
    public bool Apply(string wwwroot, IReadOnlySet<string> icons)
    {
        Match link = StylesheetLink().Match(File.ReadAllText(Path.Combine(wwwroot, "index.html")));
        if (!link.Success)
        {
            throw new InvalidOperationException($"index.html links no lib/fontawesome-<version>/{Stylesheet}.");
        }

        string folder = link.Groups["folder"].Value;
        string source = Path.Combine(wwwroot, "lib", folder);
        string sourceFonts = Path.Combine(source, "webfonts");
        (string css, IReadOnlySet<int> codepoints) = Filter(File.ReadAllText(Path.Combine(source, Stylesheet)), icons);
        string unicodes = string.Join(',', codepoints.Order().Select(codepoint => $"U+{codepoint:X4}"));

        string work = Directory.CreateTempSubdirectory("fontawesome-").FullName;
        try
        {
            foreach (string font in SubsetFonts)
            {
                if (!TrySubset(Path.Combine(sourceFonts, font), unicodes, Path.Combine(work, font)))
                {
                    return false;
                }
            }

            string targetFolder = $"{folder}-{ContentHash(css, work)}";
            WriteAssets(source, Path.Combine(wwwroot, "lib", targetFolder), work, css);
            UpdateStylesheetLinks(wwwroot, link.Value, $"lib/{targetFolder}/{Stylesheet}");
            return true;
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private static void WriteAssets(string source, string target, string subsetFonts, string css)
    {
        Directory.CreateDirectory(Path.Combine(target, "css"));
        File.WriteAllText(Path.Combine(target, Stylesheet), css);
        File.Copy(Path.Combine(source, "LICENSE.txt"), Path.Combine(target, "LICENSE.txt"), overwrite: true);

        string targetFonts = Directory.CreateDirectory(Path.Combine(target, "webfonts")).FullName;
        foreach (string font in Directory.EnumerateFiles(Path.Combine(source, "webfonts")))
        {
            string name = Path.GetFileName(font);
            string selectedFont = SubsetFonts.Contains(name) ? Path.Combine(subsetFonts, name) : font;
            File.Copy(selectedFont, Path.Combine(targetFonts, name), overwrite: true);
        }
    }

    private static void UpdateStylesheetLinks(string wwwroot, string from, string to)
    {
        foreach (string name in new[] { "index.html", PublishedSite.ShellFile })
        {
            string page = Path.Combine(wwwroot, name);
            if (File.Exists(page))
            {
                File.WriteAllText(page, File.ReadAllText(page).Replace(from, to, StringComparison.Ordinal));
            }
        }
    }

    private bool TrySubset(string font, string unicodes, string output)
    {
        ProcessStartInfo start = new(pyftsubset)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            ArgumentList = { font, $"--unicodes={unicodes}", "--flavor=woff2", $"--output-file={output}" },
        };

        using Process process = new() { StartInfo = start };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"{pyftsubset} didn't start.");
            }
        }
        catch (Win32Exception)
        {
            return false;
        }

        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{pyftsubset} failed for {Path.GetFileName(font)}: {errors}");
        }

        return true;
    }

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

    // CSS content can contain hex escapes, escaped characters, or literal text.
    private static void AddCodepoints(string content, HashSet<int> codepoints)
    {
        foreach (Match character in CssCharacter().Matches(content))
        {
            Group hex = character.Groups["hex"];
            int codepoint = hex.Success
                ? int.Parse(hex.ValueSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : char.ConvertToUtf32(content, character.Groups["char"].Index);
            codepoints.Add(codepoint);
        }
    }

    // Match the original versioned folder so subsetting always starts from the full fonts.
    [GeneratedRegex(@"lib/(?<folder>fontawesome-[0-9][0-9.]*)/css/all\.min\.css")]
    private static partial Regex StylesheetLink();

    [GeneratedRegex(@"\bfa-[a-z0-9]+(?:-[a-z0-9]+)*")]
    private static partial Regex IconClass();

    [GeneratedRegex(@"(?<selectors>\.fa-[a-z0-9-]+(?:,\.fa-[a-z0-9-]+)*)\{--fa:""(?<content>(?:\\[0-9a-fA-F]{1,6} ?|\\.|[^""\\])+)""\}")]
    private static partial Regex IconRule();

    [GeneratedRegex(@"\\(?<hex>[0-9a-fA-F]{1,6}) ?|\\?(?<char>.)", RegexOptions.Singleline)]
    private static partial Regex CssCharacter();
}
