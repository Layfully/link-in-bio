using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>Reads scalars as text and arrays as their first string entry.</summary>
/// <remarks>System.Text.Json handles JSON <c>null</c> directly.</remarks>
internal sealed class StoryblokStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString() ?? string.Empty,
        JsonTokenType.Number => reader.TryGetInt64(out long integer)
            ? integer.ToString(CultureInfo.InvariantCulture)
            : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
        JsonTokenType.True => "true",
        JsonTokenType.False => "false",
        JsonTokenType.StartArray => ReadFirstString(ref reader),
        _ => throw new JsonException($"A Storyblok value of type {reader.TokenType} can't be read as text."),
    };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => throw new NotSupportedException();

    private static string ReadFirstString(ref Utf8JsonReader reader)
    {
        string? first = null;
        // The converter must finish on the closing array token.
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                first ??= reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return first ?? string.Empty;
    }
}
