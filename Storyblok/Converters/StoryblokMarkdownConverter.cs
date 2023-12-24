using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

public class StoryblokMarkdownConverter : JsonConverter<Markdown>
{
    public override Markdown Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? @string = reader.GetString();

        return new Markdown
        {
            Value = @string
        };
    }

    public override void Write(Utf8JsonWriter writer, Markdown value, JsonSerializerOptions options) => throw new NotImplementedException();
}
