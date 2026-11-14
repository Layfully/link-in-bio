namespace Storyblok;

/// <summary>Storyblok answered that the story doesn't exist.</summary>
public sealed record StoryNotFound;

/// <summary>Storyblok couldn't be reached or answered with an error, even after retries. Trying again later may help.</summary>
public sealed record StoryblokUnavailable(Exception Error);

/// <summary>
/// Storyblok delivered the story, but its content doesn't fit the site: it isn't valid JSON, or its content block isn't
/// the expected component. Trying again doesn't help until the content or the site is fixed.
/// </summary>
public sealed record StoryUnreadable(Exception Error);

/// <summary>
/// The outcome of loading a story: the story, not found, Storyblok unavailable, or content the site can't read.
/// Switch on it with a <c>null</c> arm rather than <c>_</c>, so a new case produces a warning at every switch.
/// </summary>
public union StoryLoadResult<T>(StoryblokStory<T>, StoryNotFound, StoryblokUnavailable, StoryUnreadable) where T : StoryblokComponent;
