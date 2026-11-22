namespace Storyblok.Client;

/// <summary>Temporarily caches both stories and not-found responses.</summary>
internal sealed class StoryCache(TimeProvider time)
{
    private readonly Dictionary<string, (StoryblokStory? Story, DateTimeOffset Expires)> _entries = new(StringComparer.Ordinal);

    /// <summary>True for a cached answer that is still fresh; <paramref name="story"/> is null when the story doesn't exist.</summary>
    public bool TryGet(string path, out StoryblokStory? story)
    {
        story = null;
        if (!_entries.TryGetValue(path, out (StoryblokStory? Story, DateTimeOffset Expires) entry))
        {
            return false;
        }

        if (time.GetUtcNow() >= entry.Expires)
        {
            _entries.Remove(path);
            return false;
        }

        story = entry.Story;
        return true;
    }

    /// <summary>Keeps the answer for <paramref name="path"/>, a story or null for one that doesn't exist.</summary>
    public void Set(string path, StoryblokStory? story, TimeSpan duration) => _entries[path] = (story, time.GetUtcNow() + duration);
}
