using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client.Data.Converters;

/// <summary>Reads a Storyblok link as a safe address or returns <c>null</c>.</summary>
public sealed class StoryblokUrlConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement link = document.RootElement;

        string? address = link.ValueKind switch
        {
            JsonValueKind.String => NonEmpty(link.GetString()),
            JsonValueKind.Object => ReadObjectAddress(link),
            _ => null,
        };

        if (address is null)
        {
            return null;
        }

        address = WithoutTabsAndNewlines(address.Trim());

        if (link.ValueKind == JsonValueKind.Object
            && TextOf(link, "anchor")?.TrimStart('#') is { Length: > 0 } anchor
            && !address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            address = $"{address}#{anchor}";
        }

        return IsSafe(address) ? address : null;
    }

    private static string? ReadObjectAddress(JsonElement link) => TextOf(link, "linktype") switch
    {
        "email" => ToMailto(TextOf(link, "email") ?? TextOf(link, "url")),
        "story" => ToSitePath(TextOf(link, "cached_url") ?? TextOf(link, "url")),
        _ => TextOf(link, "url") ?? TextOf(link, "cached_url"),
    };

    // Check root-relative paths first because Uri can parse them as absolute file addresses.
    private static bool IsSafe(string address) =>
        IsSitePath(address) || address.StartsWith('#')
        || (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http" or "mailto");

    // Browsers treat both //host and /\host as links to another host.
    private static bool IsSitePath(string address) =>
        address.StartsWith('/') && !address.StartsWith("//", StringComparison.Ordinal) && !address.StartsWith("/\\", StringComparison.Ordinal);

    // Match browser parsing so a tab or newline cannot disguise a hostname or scheme.
    private static string WithoutTabsAndNewlines(string address) =>
        address.Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => throw new NotSupportedException();

    private static string? TextOf(JsonElement link, string property) =>
        link.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? NonEmpty(value.GetString()) : null;

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? ToMailto(string? email) =>
        email is null ? null : email.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? email : $"mailto:{email}";

    // Story slugs from Storyblok mapped to the site's routes: "home" is the start page, "certificates/x" is /Certificate/x.
    private static string? ToSitePath(string? slug)
    {
        if (slug is null)
        {
            return null;
        }

        slug = slug.Trim('/');
        const string Certificates = "certificates/";

        return slug switch
        {
            "" or "home" => "/",
            _ when slug.StartsWith(Certificates, StringComparison.OrdinalIgnoreCase) => $"/Certificate/{slug[Certificates.Length..]}",
            _ => $"/{slug}",
        };
    }
}
