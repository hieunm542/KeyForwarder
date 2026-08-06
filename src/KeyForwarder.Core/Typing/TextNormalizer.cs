namespace KeyForwarder.Typing;

/// <summary>
/// Normalizes clipboard text for keyboard injection.
/// </summary>
public static class TextNormalizer
{
    /// <summary>
    /// Converts CRLF/CR to LF and returns the string ready for typing.
    /// Returns empty string for null/whitespace-only? No — preserve spaces; only null → empty.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                   .Replace('\r', '\n');
    }
}
