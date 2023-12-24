using Storyblok.Client;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyblok.Converters;

public class StoryblokComponentConverter : JsonConverter<StoryblokComponent>
{
    public override StoryblokComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // performance is probably abysmal, but System.Text.Json does not support polymorphic deserialization very well
        // https://github.com/dotnet/corefx/issues/38650
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.TryGetProperty("component", out var componentElement))
        {
            string? componentName = componentElement.GetString();

            if (!string.IsNullOrWhiteSpace(componentName))
            {
                IDictionary<string, StoryblokMappings.Mapping> componentMappings = StoryblokMappings.Mappings;

                if (componentMappings.TryGetValue(componentName, out StoryblokMappings.Mapping? value))
                {
                    var mapping = value;

                    string rawText = document.RootElement.GetRawText();

                    try
                    {
                        if (JsonSerializer.Deserialize(rawText, mapping.Type, options) is not StoryblokComponent component)
                        {
                            throw new Exception($"Unable to serialize {rawText}");
                        }

                        // we don't want the "editable" property set at all, when we're not in editor
                        // this makes it easier for the client, so he does not have to check if in the editor on each component, he just has to render the "editable" stuff into it
                        if (!StoryblokBaseClient.IsInEditor)
                        {
                            component.Editable = null;
                        }

                        component.IsInEditor = StoryblokBaseClient.IsInEditor;

                        return component;
                    }
                    catch (JsonException ex)
                    {
                        throw new Exception($"Unable to deserialize ({ex.Message}): {rawText}", ex);
                    }
                }
            }
        }

        // don't call JsonSerializer.Deserizalize, because we'll get a stack overlow
        return new StoryblokComponent
        {
            Uuid = document.RootElement.GetProperty("_uid").GetGuid(),
            Component = document.RootElement.GetProperty("component").GetString() ?? string.Empty,
            IsInEditor = StoryblokBaseClient.IsInEditor
        };
    }

    public override void Write(Utf8JsonWriter writer, StoryblokComponent value, JsonSerializerOptions options) => throw new NotImplementedException();
}
