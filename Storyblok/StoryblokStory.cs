using System.Text.Json.Serialization;

namespace Storyblok;

/// <summary>A story from the Storyblok API.</summary>
public class StoryblokStory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The story's slug including its folders, used to match live edits.</summary>
    [JsonPropertyName("full_slug")]
    public string? FullSlug { get; set; }

    /// <summary>The content block. An omitted field keeps the default block, while an explicit JSON <c>null</c> clears it.</summary>
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

    /// <summary>The cache version for subsequent requests.</summary>
    [JsonPropertyName("cv")]
    public long CacheVersion { get; set; }
}
