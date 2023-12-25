using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client.Data.Converters;

public class StoryblokUrlConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument jsonTags = JsonDocument.ParseValue(ref reader);
        JsonElement jsonUrl = jsonTags.RootElement.GetProperty("url");
        return jsonUrl.GetString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => throw new NotImplementedException();
}
