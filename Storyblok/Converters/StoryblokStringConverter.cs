using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>
/// Reads Storyblok values into text fields, which Storyblok doesn't always send as JSON strings: numbers and booleans become
/// their text, and a list (a multi-option field) becomes its first text entry.
/// </summary>
/// <remarks>
/// One converter covers <c>string</c> and <c>string?</c>, which are the same type at run time. A JSON <c>null</c> never
/// reaches it: System.Text.Json sets null itself.
/// </remarks>
internal sealed class StoryblokStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;

            case JsonTokenType.Number:
                return reader.TryGetInt64(out long integer)
                    ? integer.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(CultureInfo.InvariantCulture);

            case JsonTokenType.True:
                return "true";

            case JsonTokenType.False:
                return "false";

            case JsonTokenType.StartArray:
                // A converter must finish on the array's last token, so every entry is read, not just the first.
                string? first = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (first is null && reader.TokenType == JsonTokenType.String)
                    {
                        first = reader.GetString();
                    }
                    else
                    {
                        reader.Skip(); // moves past a nested object or array; does nothing for a single value
                    }
                }

                return first ?? string.Empty;

            default:
                throw new JsonException($"A Storyblok value of type {reader.TokenType} can't be read as text.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => throw new NotSupportedException();
}
