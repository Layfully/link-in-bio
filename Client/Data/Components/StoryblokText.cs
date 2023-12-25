using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("text")]
public class StoryblokText : StoryblokComponent
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
