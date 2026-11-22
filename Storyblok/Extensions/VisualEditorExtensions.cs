using System.Text.Json;

namespace Storyblok.Extensions;

public static class VisualEditorExtensions
{
    private const string Prefix = "<!--#storyblok#";
    private const string Suffix = "-->";

    /// <summary>Creates click-to-edit attributes from a block's <c>_editable</c> marker, if present and valid.</summary>
    public static IReadOnlyDictionary<string, object>? EditorAttributes(this StoryblokComponent? block)
    {
        if (block?.Editable is not { } editable
            || !editable.StartsWith(Prefix, StringComparison.Ordinal)
           || !editable.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return null;
        }

        string options = editable[Prefix.Length..^Suffix.Length];

        try
        {
            using JsonDocument document = JsonDocument.Parse(options);
            JsonElement marker = document.RootElement;
            if (marker.ValueKind != JsonValueKind.Object
                || Text(marker, "id") is not { } id
                || Text(marker, "uid") is not { } uid)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                ["data-blok-c"] = options,
                ["data-blok-uid"] = $"{id}-{uid}",
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Storyblok writes ids as strings, but a number reads the same.
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ToString()
            : null;
}
