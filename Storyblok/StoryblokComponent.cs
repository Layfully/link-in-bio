using System.Text.Json.Serialization;

namespace Storyblok;

public class StoryblokComponent
{
    [JsonPropertyName("_uid")]
    public Guid Uuid { get; set; }
    [JsonPropertyName("_editable")]
    public string? Editable { get; set; }
    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;
    public bool IsInEditor { get; set; }
}

public abstract class StoryblokReferencedComponentContainer : StoryblokComponent
{
    public abstract StoryblokComponent[]? ContainedComponents { get; set; }
}
