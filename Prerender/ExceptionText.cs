namespace Prerender;

/// <summary>Exceptions as one line for a GitHub Actions warning.</summary>
public static class ExceptionText
{
    /// <summary>Joins distinct exception messages from the outermost exception to the innermost.</summary>
    public static string Describe(Exception exception)
    {
        if (exception.InnerException is null)
        {
            return TrimMessage(exception.Message);
        }

        List<string> messages = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            string message = TrimMessage(current.Message);
            if (message.Length > 0 && !messages.Contains(message))
            {
                messages.Add(message);
            }
        }

        return string.Join(": ", messages);
    }

    private static string TrimMessage(string message)
    {
        ReadOnlySpan<char> trimmed = message.AsSpan().Trim().TrimEnd('.');
        return trimmed.Length == message.Length ? message : trimmed.ToString();
    }
}
