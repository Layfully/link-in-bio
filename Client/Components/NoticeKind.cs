namespace Client.Components;

/// <summary>Controls how a notice is announced.</summary>
public enum NoticeKind
{
    /// <summary>Announced as an alert.</summary>
    Problem,

    /// <summary>Announced as a polite status update.</summary>
    Information,
}
