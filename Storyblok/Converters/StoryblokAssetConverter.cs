using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>Reads a Storyblok asset field. An asset field without a file (an empty string or an asset without a URL) is read as null.</summary>
internal sealed class StoryblokAssetConverter : JsonConverter<StoryblokAsset?>
{
    public override StoryblokAsset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (reader.GetString() is { Length: > 0 } text)
            {
                throw new JsonException($"\"{text}\" is not a Storyblok asset.");
            }

            return null;
        }

        // Without the original options: they contain this converter, which would call itself forever.
        StoryblokAsset? asset = JsonSerializer.Deserialize<StoryblokAsset>(ref reader);

        return string.IsNullOrWhiteSpace(asset?.Url) ? null : asset;
    }

    public override void Write(Utf8JsonWriter writer, StoryblokAsset? value, JsonSerializerOptions options) => throw new NotSupportedException();
}
