using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

public class StoryblokStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return string.Empty;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32().ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read(); // read start array

            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return string.Empty;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? arrayStringValue = reader.GetString();
                return arrayStringValue ?? string.Empty;
            }

            throw new Exception("Unable to deserialize array into string.");
        }

        string? stringValue = reader.GetString();
        return stringValue ?? string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => throw new NotImplementedException();
}

public class StoryblokNullableStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32().ToString(CultureInfo.InvariantCulture);
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read(); // read start array

            if (reader.TokenType == JsonTokenType.EndArray)
            {
                reader.Read(); // read end array
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? arrayStringValue = reader.GetString();
                reader.Read(); // read end array
                return arrayStringValue;
            }

            reader.Read(); // read start array
            throw new Exception("Unable to deserialize array into string.");
        }

        string? stringValue = reader.GetString();
        return stringValue;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => throw new NotImplementedException();
}
