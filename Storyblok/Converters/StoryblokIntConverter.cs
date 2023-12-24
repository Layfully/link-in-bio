using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

public class StoryblokIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        string? @string = reader.GetString();

        if (int.TryParse(@string, NumberStyles.Any, CultureInfo.InvariantCulture, out var @int))
        {
            return @int;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => throw new NotImplementedException();
}

public class StoryblokNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        string? @string = reader.GetString();
        if (int.TryParse(@string, NumberStyles.Any, CultureInfo.InvariantCulture, out var @int))
        {
            return @int;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options) => throw new NotImplementedException();
}
