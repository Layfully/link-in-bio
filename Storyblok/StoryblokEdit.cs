using System.Diagnostics.CodeAnalysis;

namespace Storyblok;

/// <summary>A live story update from the Visual Editor.</summary>
public sealed class StoryblokEdit(StoryblokStory story)
{
    /// <summary>
    /// Gets the edited story when its slug and content type match.
    /// Slug matching ignores case and leading or trailing slashes.
    /// </summary>
    public bool TryGet<T>(string slug, [NotNullWhen(true)] out StoryblokStory<T>? edited) where T : StoryblokComponent
    {
        edited = story.Content is T content
            && string.Equals(slug.Trim('/'), story.FullSlug?.Trim('/'), StringComparison.OrdinalIgnoreCase)
                ? new StoryblokStory<T>(story.Name, content)
                : null;
        return edited is not null;
    }
}
