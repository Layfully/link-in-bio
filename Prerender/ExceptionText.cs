namespace Prerender;

/// <summary>Exceptions as one line for a GitHub Actions warning.</summary>
public static class ExceptionText
{
    /// <summary>
    /// The message of the exception and of each exception inside it: the outer one says what failed, the inner ones why
    /// ("Storyblok is unavailable: Response status code does not indicate success: 401 (Unauthorized)").
    /// </summary>
    public static string Describe(Exception exception)
    {
        List<string> messages = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message.Trim().TrimEnd('.'));
        }

        return string.Join(": ", messages.Where(message => message.Length > 0).Distinct(StringComparer.Ordinal));
    }
}
