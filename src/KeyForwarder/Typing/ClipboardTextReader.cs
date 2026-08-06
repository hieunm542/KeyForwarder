namespace KeyForwarder.Typing;

public static class ClipboardTextReader
{
    /// <summary>
    /// Reads Unicode text from the clipboard. Must be called on an STA thread.
    /// Returns null if clipboard has no text.
    /// </summary>
    public static string? TryReadText()
    {
        try
        {
            if (!Clipboard.ContainsText(TextDataFormat.UnicodeText) && !Clipboard.ContainsText())
            {
                return null;
            }

            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrEmpty(text))
            {
                text = Clipboard.GetText();
            }

            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
