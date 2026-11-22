namespace Client.Components;

/// <summary>Controls what the Storyblok status component displays.</summary>
public enum ContentStatus
{
    /// <summary>Show a placeholder and loading hint.</summary>
    Loading,

    /// <summary>Let the page display the result.</summary>
    Done,

    /// <summary>Show a retry notice when Storyblok is unavailable.</summary>
    Unavailable,

    /// <summary>Show a notice when the published content can't be read.</summary>
    Unreadable,
}
