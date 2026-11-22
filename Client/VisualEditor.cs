namespace Client;

/// <summary>Recognizes a page opened by Storyblok's Visual Editor.</summary>
public static class VisualEditor
{
    /// <summary>Checks for the exact <c>_storyblok</c> query parameter, with or without a value.</summary>
    public static bool HasOpened(Uri address) =>
        address.Query.TrimStart('?').Split('&')
            .Any(parameter => parameter.Split('=', 2)[0] == "_storyblok");
}
