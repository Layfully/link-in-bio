using System.Text.Json.Serialization;

namespace Storyblok;

/// <summary>The base type for Storyblok blocks and the fallback for unregistered component names.</summary>
public class StoryblokComponent
{
    [JsonPropertyName("_uid")]
    public Guid Uuid { get; set; }

    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;

    /// <summary>The draft block's click-to-edit marker for the Visual Editor.</summary>
    [JsonPropertyName("_editable")]
    public string? Editable { get; set; }
}
