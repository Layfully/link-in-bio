namespace Client;

/// <summary>Recognizes a page opened by Storyblok's Visual Editor.</summary>
public static class VisualEditor
{
    /// <summary>
    /// True when the address carries the editor's <c>_storyblok</c> parameter, with or without a value. Parameters that only
    /// start with the same letters, such as <c>_storyblok_tk[token]</c>, don't count on their own.
    /// </summary>
    public static bool HasOpened(Uri address) =>
        address.Query.TrimStart('?').Split('&').Any(parameter => parameter == "_storyblok" || parameter.StartsWith("_storyblok=", StringComparison.Ordinal));
}
