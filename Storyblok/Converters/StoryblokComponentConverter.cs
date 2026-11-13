using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

/// <summary>
/// Reads a Storyblok block into the class registered for its <c>component</c> name.
/// System.Text.Json's own polymorphism can't do this: it needs the type name as the object's first property and the
/// derived classes declared on the base class, while Storyblok puts <c>component</c> anywhere and the classes live in the app.
/// </summary>
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
            // This converter only handles StoryblokComponent itself, never a class derived from it, so the registered
            // class is read with the normal property mapping instead of coming back here.
            return block.Deserialize(type, options) as StoryblokComponent
                ?? throw new JsonException($"The Storyblok component \"{name}\" could not be read into {type}.");
        }

        // A block the app has no class for, such as one added in Storyblok before the site knows it. Pages pick their
        // blocks by type, so it is skipped rather than failing the whole story.
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
