using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>
/// Reads Storyblok dates: story timestamps such as <c>2024-05-01T10:00:00.000Z</c> and date fields such as <c>2024-05-01 10:00</c>.
/// Both are UTC, and stay UTC (<see cref="DateTimeKind.Utc"/>) instead of being converted to the device's time zone.
/// A value that isn't a date is read as no date.
/// </summary>
internal sealed class StoryblokDateConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        StoryblokDate.Parse(ref reader) ?? default;

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => throw new NotSupportedException();
}

/// <inheritdoc cref="StoryblokDateConverter"/>
internal sealed class StoryblokNullableDateConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        StoryblokDate.Parse(ref reader);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options) => throw new NotSupportedException();
}

internal static class StoryblokDate
{
    private const DateTimeStyles Utc = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public static DateTime? Parse(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip(); // a converter must finish on the value's last token, also for an object or a list
            return null;
        }

        string? text = reader.GetString();

        if (DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, Utc, out DateTime date)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, Utc, out date))
        {
            return date;
        }

        return null;
    }
}
