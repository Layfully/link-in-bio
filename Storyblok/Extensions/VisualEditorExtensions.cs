using System.Text.Json;

namespace Storyblok.Extensions;

public static class VisualEditorExtensions
{
    private const string Prefix = "<!--#storyblok#";
    private const string Suffix = "-->";

    /// <summary>
    /// The attributes that let the Visual Editor's bridge find a block in the page, so clicking it opens its fields:
    /// <c>data-blok-c</c> and <c>data-blok-uid</c>, made from the block's <c>_editable</c> value the way Storyblok's own
    /// SDKs make them (written by hand here: their packages would add a download for every visitor). Null when the block
    /// has no usable value, which is always the case outside the editor: only drafts carry one.
    /// </summary>
    public static IReadOnlyDictionary<string, object>? EditorAttributes(this StoryblokComponent? block)
    {
        if (block?.Editable is not { } editable
            || !editable.StartsWith(Prefix, StringComparison.Ordinal)
            || !editable.EndsWith(Suffix, StringComparison.Ordinal)
            || editable.Length < Prefix.Length + Suffix.Length)
        {
            return null;
        }

        string options = editable[Prefix.Length..^Suffix.Length];

        try
        {
            using JsonDocument document = JsonDocument.Parse(options);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || Text(document.RootElement, "id") is not { } id
                || Text(document.RootElement, "uid") is not { } uid)
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
            // A value the editor wrote differently than expected only costs click-to-edit on that block.
            return null;
        }
    }

    // Storyblok writes ids as strings, but a number reads the same.
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? value.ToString()
            : null;
}
