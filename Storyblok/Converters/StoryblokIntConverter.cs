using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>Reads Storyblok numbers, which number fields send as text (<c>"42"</c>). Anything else is read as 0.</summary>
internal sealed class StoryblokIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        StoryblokInt.Parse(ref reader) ?? default;

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => throw new NotSupportedException();
}

/// <summary>Reads Storyblok numbers, which number fields send as text (<c>"42"</c>). Anything else, an empty field included, is read as no number.</summary>
internal sealed class StoryblokNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        StoryblokInt.Parse(ref reader);

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options) => throw new NotSupportedException();
}

internal static class StoryblokInt
{
    public static int? Parse(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out int number) ? number : null;

            case JsonTokenType.String:
                return int.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;

            default:
                reader.Skip(); // a converter must finish on the value's last token, also for an object or a list
                return null;
        }
    }
}
