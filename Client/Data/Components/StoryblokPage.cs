using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("page")]
public sealed class StoryblokPage : StoryblokComponent
{
    [JsonPropertyName("body")]
    public StoryblokComponent[]? Body { get; set; }
}
