namespace Client.Components;

/// <summary>Where a page's Storyblok content is, which decides what <c>StoryblokStatus</c> shows.</summary>
public enum ContentStatus
{
    /// <summary>Still being requested: a placeholder and a loading hint.</summary>
    Loading,

    /// <summary>The request finished and the page shows its own outcome (the content, "not found" or "nothing here yet").</summary>
    Done,

    /// <summary>Storyblok couldn't deliver the content, even after retries: a notice with a retry button.</summary>
    Unavailable,
}
