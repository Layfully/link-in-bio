namespace Storyblok.Client;

/// <summary>
/// Stories loaded during a visit, and stories Storyblok said don't exist, each kept for a while. A dictionary rather than
/// Microsoft.Extensions.Caching.Memory: the site caches one or two stories, and the library is two more assemblies for
/// every visitor to download and load.
/// </summary>
internal sealed class StoryCache(TimeProvider time)
{
    private readonly Dictionary<string, (StoryblokStory? Story, DateTimeOffset Expires)> _entries = new(StringComparer.Ordinal);

    /// <summary>True for a cached answer that is still fresh; <paramref name="story"/> is null when the story doesn't exist.</summary>
    public bool TryGet(string path, out StoryblokStory? story)
    {
        if (_entries.TryGetValue(path, out (StoryblokStory? Story, DateTimeOffset Expires) entry) && time.GetUtcNow() < entry.Expires)
        {
            story = entry.Story;
            return true;
        }

        _entries.Remove(path);
        story = null;
        return false;
    }

    /// <summary>Keeps the answer for <paramref name="path"/>, a story or null for one that doesn't exist.</summary>
    public void Set(string path, StoryblokStory? story, TimeSpan duration) => _entries[path] = (story, time.GetUtcNow() + duration);
}
