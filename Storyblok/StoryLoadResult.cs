namespace Storyblok;

/// <summary>Storyblok answered that the story doesn't exist.</summary>
public sealed record StoryNotFound;

/// <summary>Storyblok couldn't be reached or answered with an error, even after retries.</summary>
public sealed record StoryblokUnavailable(Exception Error);

/// <summary>
/// The outcome of loading a story: the story, not found, or Storyblok unavailable.
/// Switch on it with a <c>null</c> arm rather than <c>_</c>, so a new case produces a warning at every switch.
/// </summary>
public union StoryLoadResult<T>(StoryblokStory<T>, StoryNotFound, StoryblokUnavailable) where T : StoryblokComponent;
