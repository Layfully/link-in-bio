using System.Diagnostics.CodeAnalysis;

namespace Storyblok;

/// <summary>A story as it is being edited in the Visual Editor (see <see cref="Client.StoryblokStoryClient.StoryEdited"/>).</summary>
public sealed class StoryblokEdit(StoryblokStory story)
{
    /// <summary>
    /// The edited story, when it is the story under <paramref name="slug"/> and its content is a <typeparamref name="T"/>.
    /// Slashes around the slug and case don't count: a certificate's address may spell its name differently from its slug.
    /// </summary>
    public bool TryGet<T>(string slug, [NotNullWhen(true)] out StoryblokStory<T>? edited) where T : StoryblokComponent
    {
        if (story.Content is T content && string.Equals(slug.Trim('/'), story.FullSlug?.Trim('/'), StringComparison.OrdinalIgnoreCase))
        {
            edited = new StoryblokStory<T>(story.Name, content);
            return true;
        }

        edited = null;
        return false;
    }
}
