using System.Text.Json.Serialization;

namespace Storyblok;

/// <summary>A story as Storyblok delivers it. Only the fields the site uses are read.</summary>
public class StoryblokStory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The story's content block. Null when Storyblok sends <c>"content": null</c>, which System.Text.Json sets without asking a converter.</summary>
    [JsonPropertyName("content")]
    public StoryblokComponent? Content { get; set; } = new();
}

/// <summary>A story whose content block is the component class <typeparamref name="T"/>.</summary>
public sealed class StoryblokStory<T>(string? name, T content) where T : StoryblokComponent
{
    public string? Name { get; } = name;

    public T Content { get; } = content;
}

internal sealed class StoryblokStoryContainer
{
    [JsonPropertyName("story")]
    public StoryblokStory? Story { get; set; }

    /// <summary>The cache version Storyblok returned with this response. It is passed back as <c>cv</c> on later requests.</summary>
    [JsonPropertyName("cv")]
    public long CacheVersion { get; set; }
}
