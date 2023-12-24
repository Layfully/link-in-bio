using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

public class StoryblokAssetConverter : JsonConverter<StoryblokAsset?>
{
    public override StoryblokAsset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // if we have an asset without an assigned file, it may be included as an empty string in the JSON
        if (reader.TokenType == JsonTokenType.String)
        {
            string? s = reader.GetString();

            if (s is not { Length: > 0 })
            {
                return null;
            }

            throw new Exception($"Unable to deserialize \"{s}\" to an asset.");
        }

        // don't use the original options because this will result in an endless loop
        StoryblokAsset? result = JsonSerializer.Deserialize<StoryblokAsset>(ref reader);

        if (string.IsNullOrWhiteSpace(result?.Url))
        {
            return null;
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, StoryblokAsset? value, JsonSerializerOptions options) => throw new NotImplementedException();
}
