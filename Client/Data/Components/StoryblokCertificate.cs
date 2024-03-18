using Storyblok;
using Storyblok.Attributes;
using System.Text.Json.Serialization;

namespace Client.Data.Components;

[StoryblokComponent("certificate")]
public class StoryblokCertificate : StoryblokComponent
{
    [JsonPropertyName("image")]
    public StoryblokAsset? Image { get; set; }

}
