using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client.Data.Converters;

/// <summary>
/// Turns a Storyblok link field into the address the site links to. The field is an object whose shape depends on its
/// <c>linktype</c>: a web address, another story in the space, an email address or an uploaded asset.
/// Anything unexpected becomes <c>null</c> (rendered as an entry without a link) instead of failing the whole story.
/// </summary>
public sealed class StoryblokUrlConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement link = document.RootElement;

        if (link.ValueKind == JsonValueKind.String)
        {
            return NonEmpty(link.GetString())?.Trim() is { } text && IsSafe(text) ? text : null;
        }

        if (link.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? address = TextOf(link, "linktype") switch
        {
            "email" => ToMailto(TextOf(link, "email") ?? TextOf(link, "url")),
            "story" => ToSitePath(TextOf(link, "cached_url") ?? TextOf(link, "url")),
            _ => TextOf(link, "url") ?? TextOf(link, "cached_url"), // "url", "asset" and unknown kinds
        };

        if (address is null)
        {
            return null;
        }

        address = address.Trim();

        // A link may point at a section of the target page. Editors may type the anchor with or without "#";
        // an email address has no sections.
        if (TextOf(link, "anchor")?.TrimStart('#') is { Length: > 0 } anchor && !address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            address = $"{address}#{anchor}";
        }

        return IsSafe(address) ? address : null;
    }

    // Only site-relative, web and email addresses. Anything else (javascript:, data:, ...) could run code when clicked;
    // the Content Security Policy blocks that too, but this converter shouldn't depend on it.
    // Relative addresses are checked first: in the browser, "/x" would otherwise parse as an absolute file: address.
    // "//host" and "/\host" are not site-relative: browsers read both as an address on another site.
    private static bool IsSafe(string address) =>
        IsSitePath(address) || address.StartsWith('#')
        || (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http" or "mailto");

    private static bool IsSitePath(string address) =>
        address.StartsWith('/') && !address.StartsWith("//", StringComparison.Ordinal) && !address.StartsWith("/\\", StringComparison.Ordinal);

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
