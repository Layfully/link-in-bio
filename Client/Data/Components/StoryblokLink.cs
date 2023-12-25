using Client.Data.Converters;
using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("link")]
public class StoryblokLink : StoryblokComponent
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("css_class")]
    public string? CssClass { get; set; }

    [JsonPropertyName("url")]
    [JsonConverter(typeof(StoryblokUrlConverter))]
    public string? Url { get; set; }
}
