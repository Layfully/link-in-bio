using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>Reads a Storyblok asset, returning <c>null</c> when it has no file.</summary>
internal sealed class StoryblokAssetConverter : JsonConverter<StoryblokAsset?>
{
    public override StoryblokAsset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() is { Length: > 0 } text
                ? throw new JsonException($"\"{text}\" is not a Storyblok asset.")
                : null;
        }

        // Use default options to avoid invoking this converter recursively.
        StoryblokAsset? asset = JsonSerializer.Deserialize<StoryblokAsset>(ref reader);

        return string.IsNullOrWhiteSpace(asset?.Url) ? null : asset;
    }

    public override void Write(Utf8JsonWriter writer, StoryblokAsset? value, JsonSerializerOptions options) => throw new NotSupportedException();
}
