namespace Storyblok;

/// <summary>The requested story does not exist.</summary>
public sealed record StoryNotFound;

/// <summary>A service or request failure that may succeed on retry.</summary>
public sealed record StoryblokUnavailable(Exception Error);

/// <summary>Invalid JSON or a content block of the wrong type.</summary>
public sealed record StoryUnreadable(Exception Error);

/// <summary>The story or a typed failure result.</summary>
/// <remarks>Use an explicit <c>null</c> arm in switches to preserve exhaustiveness checks.</remarks>
public union StoryLoadResult<T>(StoryblokStory<T>, StoryNotFound, StoryblokUnavailable, StoryUnreadable) where T : StoryblokComponent;
