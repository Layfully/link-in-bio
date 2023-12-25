using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("list")]
public class StoryblokList : StoryblokComponent
{
    [JsonPropertyName("entries")]
    public StoryblokComponent[]? Entries { get; set; }
}

public class StoryblokList<T> : StoryblokComponent where T : StoryblokComponent
{
    public List<T>? Entries { get; set; }
}