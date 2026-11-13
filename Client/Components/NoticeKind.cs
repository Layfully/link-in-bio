namespace Client.Components;

/// <summary>What a <c>Notice</c> tells the visitor, which decides how urgently screen readers announce it.</summary>
public enum NoticeKind
{
    /// <summary>Something went wrong. Announced immediately (<c>role="alert"</c>).</summary>
    Problem,

    /// <summary>Neutral information, such as "nothing here". Announced when the screen reader is idle (<c>role="status"</c>).</summary>
    Information,
}
