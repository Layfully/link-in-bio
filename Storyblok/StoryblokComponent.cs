using System.Text.Json.Serialization;

namespace Storyblok;

/// <summary>
/// A block from Storyblok. Blocks the app has a class for are read into that class (see <see cref="StoryblokComponentTypes"/>);
/// any other block is kept as this base type with only its id and name.
/// </summary>
public class StoryblokComponent
{
    [JsonPropertyName("_uid")]
    public Guid Uuid { get; set; }

    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;
}
