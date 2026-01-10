using System.Text;

namespace Llm64.Wasm.Services;

public record Base64Mode(string Chars, char? PaddingChar, char WhitespaceChar, char LinebreakChar)
{
    // RFC4648
    public static readonly Base64Mode Standard =
        new Base64Mode("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", '=', '+', '/');
    // RFC4648 Section 5
    public static readonly Base64Mode UrlSafe =
        new Base64Mode("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_", null, '_', '-');
}

public class Base64Helper
{
    public const int StandardLineWidth = 76; // RFC 2045 standard for email

    private readonly Base64Mode _mode;
    private readonly HashSet<char> _base64CharSet;

    public Base64Helper(Base64Mode? mode = null)
    {
        _mode = mode ?? Base64Mode.Standard;
        _base64CharSet = new(_mode.Chars);
    }

    /// <summary>
    /// Formats a base64 string with line breaks at the specified width.
    /// </summary>
    /// <param name="base64">The base64 string to format</param>
    /// <param name="lineWidth">Characters per line (default: 76 for RFC 2045)</param>
    /// <returns>Formatted base64 string with line breaks</returns>
    public string FormatWithLineBreaks(string base64, int lineWidth = StandardLineWidth)
    {
        var result = new StringBuilder();

        for (int i = 0; i < base64.Length; i += lineWidth)
        {
            int length = Math.Min(lineWidth, base64.Length - i);
            result.AppendLine(base64.Substring(i, length));
        }

        return result.ToString();
    }

    /// <summary>
    /// Checks if a character is valid for the current base64 mode.
    /// </summary>
    public bool IsValidBase64Char(char c) => _base64CharSet.Contains(c);

    /// <summary>
    /// Manually decodes a base64 string to bytes.
    /// Uses custom decoding to avoid issues with .NET's standard Convert.FromBase64String
    /// when dealing with padding characters in round-trip scenarios.
    /// </summary>
    public byte[] ManuallyDecode(string text)
    {
        // Convert each character to its 6-bit value
        var bits = new List<bool>();
        foreach (var c in text)
        {
            if (c == _mode.PaddingChar || char.IsWhiteSpace(c)) continue; // Skip padding and spaces

            var value = _mode.Chars.IndexOf(c);
            if (value == -1)
            {
                // Invalid base64 character, skip or use 0
                value = 0;
            }

            // Add 6 bits
            for (var i = 5; i >= 0; i--)
            {
                bits.Add((value & (1 << i)) != 0);
            }
        }

        // Convert bits to bytes (8 bits per byte)
        var bytes = new List<byte>();
        for (var i = 0; i + 7 < bits.Count; i += 8)
        {
            byte b = 0;
            for (var j = 0; j < 8; j++)
            {
                if (bits[i + j])
                {
                    b |= (byte)(1 << (7 - j));
                }
            }
            bytes.Add(b);
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// Normalizes text to contain only valid base64 characters.
    /// Whitespace is converted to the mode's whitespace character.
    /// Invalid characters are removed.
    /// </summary>
    public List<string> NormalizeText(string text)
    {
        var lines = new List<string>();

        // Split by newlines (preserve line breaks)
        var inputLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var line in inputLines)
        {
            // Normalize each line: keep only valid base64 characters
            var chars = new List<char>();
            foreach (var c in line)
            {
                char? appendChar = c switch
                {
                    _ when char.IsWhiteSpace(c) => _mode.WhitespaceChar,
                    _ when _base64CharSet.Contains(c) => c,
                    _ => null
                };

                if (appendChar.HasValue) chars.Add(appendChar.Value);
            }

            lines.Add(new string(chars.ToArray()));
        }

        return lines;
    }

    /// <summary>
    /// Gets the Base64Mode being used by this helper.
    /// </summary>
    public Base64Mode Mode => _mode;
}
