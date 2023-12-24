using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

public class StoryblokDateConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? @string = reader.GetString();

        if (DateTime.TryParseExact(@string, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate;
        }

        if (DateTime.TryParse(@string, out parsedDate))
        {
            return parsedDate;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => throw new NotImplementedException();
}

public class StoryblokNullableDateConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? @string = reader.GetString();
        if (DateTime.TryParseExact(@string, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return parsedDate;
        }

        if (DateTime.TryParse(@string, out parsedDate))
        {
            return parsedDate;
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options) => throw new NotImplementedException();
}
