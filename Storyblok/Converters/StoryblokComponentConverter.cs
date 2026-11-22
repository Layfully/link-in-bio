using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>Reads Storyblok blocks using the app's component registry.</summary>
internal sealed class StoryblokComponentConverter(StoryblokComponentTypes componentTypes) : JsonConverter<StoryblokComponent>
{
    public override StoryblokComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement block = document.RootElement;

        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"A Storyblok component must be a JSON object, not {block.ValueKind}.");
        }

        string? name = StringProperty(block, "component");

        if (name is not null && componentTypes.TryGetType(name, out Type? type))
        {
            // Registered subclasses use normal property mapping.
            return block.Deserialize(type, options) as StoryblokComponent
                ?? throw new JsonException($"The Storyblok component \"{name}\" could not be read into {type}.");
        }

        // Keep unknown blocks as base components so pages can skip them by type.
        return new StoryblokComponent
        {
            Uuid = Guid.TryParse(StringProperty(block, "_uid"), out Guid uuid) ? uuid : Guid.Empty,
            Component = name ?? string.Empty,
        };
    }

    public override void Write(Utf8JsonWriter writer, StoryblokComponent value, JsonSerializerOptions options) => throw new NotSupportedException();

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
