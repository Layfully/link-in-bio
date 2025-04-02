using System.Text.Json.Serialization;

namespace Storyblok;

public class StoryblokStory<T> : StoryblokStory where T : StoryblokComponent
{
    public StoryblokStory(StoryblokStory story)
    {
        Name = story.Name;
        Slug = story.Slug;
        FullSlug = story.FullSlug;
        CreatedAt = story.CreatedAt;
        PublishedAt = story.PublishedAt;
        FirstPublishedAt = story.FirstPublishedAt;
        Id = story.Id;
        Uuid = story.Uuid;
        IsStartPage = story.IsStartPage;
        Position = story.Position;
        Language = story.Language;
        TranslatedSlugs = story.TranslatedSlugs;
        DefaultFullSlug = story.DefaultFullSlug;
        TagList = story.TagList;

        T? castContent = story.Content as T;
        Content = castContent ?? throw new Exception($"A component of type \"{story.Content.GetType()}\" cannot be cast to \"{typeof(T)}\"");
    }

    [JsonPropertyName("content")]
    public new T? Content { get; set; }
}

public class StoryblokStory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
    [JsonPropertyName("full_slug")]
    public string? FullSlug { get; set; }
    [JsonPropertyName("default_full_slug")]
    public string? DefaultFullSlug { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }
    [JsonPropertyName("first_published_at")]
    public DateTime? FirstPublishedAt { get; set; }
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [JsonPropertyName("uuid")]
    public Guid Uuid { get; set; }
    [JsonPropertyName("content")]
    public StoryblokComponent Content { get; set; } = new();
    [JsonPropertyName("position")]
    public int Position { get; set; }
    [JsonPropertyName("is_startpage")]
    public bool IsStartPage { get; set; }

    [JsonPropertyName("lang")]
    public string Language { get; set; } = string.Empty;
    [JsonPropertyName("translated_slugs")]
    public IList<StoryblokTranslatedSlug> TranslatedSlugs { get; set; } = new List<StoryblokTranslatedSlug>();

    [JsonPropertyName("tag_list")]
    public List<string>? TagList { get; set; }
    public DateTime LoadedAt { get; set; }

    public override string ToString()
    {
        return FullSlug ?? string.Empty;
    }
}

public class StoryblokTranslatedSlug
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("lang")]
    public string? Language { get; set; }
}

public class StoryblokStoriesContainer
{
    [JsonPropertyName("stories")]
    public IEnumerable<StoryblokStory> Stories { get; init; } = [];
}

public class StoryblokStoryContainer
{
    [JsonPropertyName("story")]
    public StoryblokStory Story { get; set; } = new();
}
