using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("header")]
public sealed class StoryblokHeader : StoryblokComponent
{
    [JsonPropertyName("profile")]
    public StoryblokAsset? Profile { get; set; }

    [JsonPropertyName("header_link")]
    public IReadOnlyList<StoryblokLink>? Links { get; set; }

    [JsonPropertyName("typed_texts")]
    public IReadOnlyList<StoryblokText>? TypedTexts { get; set; }

    public StoryblokLink? ProfileLink => Links?.FirstOrDefault();
}
