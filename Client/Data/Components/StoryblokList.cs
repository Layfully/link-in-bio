using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("list")]
public sealed class StoryblokList : StoryblokComponent
{
    [JsonPropertyName("entries")]
    public StoryblokComponent[]? Entries { get; set; }

    public IReadOnlyList<T> EntriesOf<T>() where T : StoryblokComponent => Entries?.OfType<T>().ToList() ?? [];
}
