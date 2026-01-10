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
};

public enum EmbeddingStyle
{
    /// <summary>
    /// RFC 2045 : formatted as 76-character blocks separated by newlines
    /// eg:
    /// Foo+++++++++
    /// Bar+Baz+++++
    /// </summary>
    Email,
    /// <summary>
    /// Minimal padding used for inline data: urls
    /// eg:
    /// +++Foo/Bar+Baz+++
    /// </summary>
    Compact,
}

public class PngTool
{
    public PngTool(Base64Mode? mode)
    {
        _mode = mode ?? Base64Mode.Standard;
        _base64CharSet = new(_mode.Chars);
    }

    private readonly Base64Mode _mode;
    private readonly HashSet<char> _base64CharSet;
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    const int Base64DisplayLineWidth = 76;
    private const string PngHeaderChunkType = "IHDR";
    
    public bool IsPng(byte[] data) => (data.Length >= PngSignature.Length) && PngSignature.SequenceEqual(data.Take(PngSignature.Length));
    
    public List<string> NormalizePrompt(string prompt)
    {
        var lines = new List<string>();

        // Split by newlines (preserve line breaks)
        var inputLines = prompt.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        foreach (var line in inputLines)
        {
            // Normalize each line: keep only valid base64 characters
            var chars = new List<char>();
            foreach (char c in line)
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

    private string FormatLinesForEmail(List<string> lines)
    {
        var sb = new StringBuilder();
        var empty = new string(_mode.WhitespaceChar, Base64DisplayLineWidth);
        
        // three header lines
        sb.Append(empty);
        sb.Append(empty);
        sb.Append(empty);
        
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                sb.Append(empty);
            }
            else if (line.Length <= Base64DisplayLineWidth)
            {
                sb.Append(line.PadRight(Base64DisplayLineWidth, _mode.WhitespaceChar));
            }
            else
            {
                // poor-man's wrap (maybe we do word breaks one day)
                for (int i = 0; i < line.Length; i += Base64DisplayLineWidth)
                {
                    int length = Math.Min(Base64DisplayLineWidth, line.Length - i);
                    string segment = line.Substring(i, length);
                    sb.Append(segment.PadRight(Base64DisplayLineWidth, _mode.WhitespaceChar));
                }
            }
        }
        // three footer lines
        sb.Append(empty);
        sb.Append(empty);
        sb.Append(empty);

        return sb.ToString();
    }
    
    private string FormatLinesForCompact(List<string> lines)
    {
        var sb = new StringBuilder();
        
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                sb.Append(_mode.LinebreakChar);
            }
            else
            {
                // Pad to 76 chars
                sb.Append(line);
                sb.Append(_mode.LinebreakChar);
            }
        }

        return sb.ToString();
    }

    public byte[] EmbedTextInPng(byte[] pngData, string text, EmbeddingStyle style)
    {
        if (!IsPng(pngData)) throw new ArgumentException("Invalid PNG file", nameof(pngData));

        // Find IHDR chunk
        int ihdrIndex = FindChunk(pngData, PngHeaderChunkType);
        if (ihdrIndex == -1) throw new Exception("Invalid PNG: Header chunk not found");

        // IHDR chunk structure: length(4) + type(4) + data(13) + CRC(4) = 25 bytes
        int insertPosition = ihdrIndex + 25;

        // Normalize and process lines for proper base64 layout
        var normalizedLines = NormalizePrompt(text);
        var embeddedText = style switch
        {
            EmbeddingStyle.Email => FormatLinesForEmail(normalizedLines),
            EmbeddingStyle.Compact => FormatLinesForCompact(normalizedLines),
            _ => throw new InvalidOperationException()
        };
        // Insert empty buffer, then overwrite with correct bytes
        return InsertChunk(pngData, embeddedText, insertPosition);
    }

    private static int FindChunk(byte[] data, string chunkType)
    {
        byte[] typeBytes = Encoding.ASCII.GetBytes(chunkType);
        for (int i = 8; i < data.Length - 4; i++)
        {
            if (data[i] == typeBytes[0] && data[i + 1] == typeBytes[1] &&
                data[i + 2] == typeBytes[2] && data[i + 3] == typeBytes[3])
            {
                return i - 4; // Return position of length field
            }
        }
        return -1;
    }

    private static byte[] GeneratePadding(int alignment, int finalByteSize)
    {
        byte[] pattern = [0xFB, 0xEF, 0xBE];
        
        // The pattern 0xFB 0xEF 0xBE encodes to "++++" ONLY at 3-byte boundaries
        // So add some bytes to align
        var result = alignment switch
        {
            0 => new List<byte>(),
            1 => new List<byte>([0x00, 0x00]),
            2 => new List<byte>([0x00]),
            _ => throw new InvalidOperationException()
        };

        // Now add the repeating pattern
        int remainingBytes = finalByteSize - result.Count;
        
        for (int i = 0; i < remainingBytes; i += 3)
        {
            int copyLen = Math.Min(3, remainingBytes - i);
            for (int j = 0; j < copyLen; j++)
            {
                result.Add(pattern[j]);
            }
        }

        return result.ToArray();
    }

    private byte[] InsertChunk(byte[] pngData, string text, int insertPosition)
    {
        // Ensure text length is a multiple of 4 for proper base64 encoding
        int mod = text.Length % 4;
        if (mod != 0)
        {
            text += new string(_mode.WhitespaceChar, 4 - mod);
        }

        // Decode text to raw bytes
        byte[] rawBytes = ManuallyDecodeBase64(text);

        // Calculate where the buffer will start in the PNG
        // Chunk structure: length(4) + type(4) + data(N) + CRC(4)
        int bufferStartInPng = insertPosition + 8; // After length and type fields

        // Calculate padding needed to align to 57-byte boundary
        // Standard base64 wraps at 76 chars = 57 bytes
        int currentPosition = bufferStartInPng;
        int paddingToLineStart = (57 - (currentPosition % 57)) % 57;
        
        // Calculate total buffer size
        int totalBufferSize = paddingToLineStart + rawBytes.Length;

        // Create empty buffer (all zeros)
        byte[] emptyBuffer = new byte[totalBufferSize];

        // Build the chunk with empty buffer
        byte[] chunk = BuildChunk("sLOP", emptyBuffer);

        // Insert the chunk into the PNG
        byte[] tempPng = new byte[pngData.Length + chunk.Length];
        Array.Copy(pngData, 0, tempPng, 0, insertPosition);
        Array.Copy(chunk, 0, tempPng, insertPosition, chunk.Length);
        Array.Copy(pngData, insertPosition, tempPng, insertPosition + chunk.Length, pngData.Length - insertPosition);

        // Now overwrite the buffer with actual content
        int writePosition = bufferStartInPng;

        // Generate padding before text (alignment-aware for base64)
        byte[] paddingBeforeBytes = GeneratePadding(writePosition % 3, paddingToLineStart);
        Array.Copy(paddingBeforeBytes, 0, tempPng, writePosition, paddingBeforeBytes.Length);
        writePosition += paddingBeforeBytes.Length;

        // Write the raw bytes
        Array.Copy(rawBytes, 0, tempPng, writePosition, rawBytes.Length);
        
        // Recalculate CRC for the modified chunk
        int crcPosition = insertPosition + 8 + totalBufferSize; // After length, type, and data
        byte[] typeAndData = new byte[4 + totalBufferSize];
        Array.Copy(Encoding.ASCII.GetBytes("sLOP"), 0, typeAndData, 0, 4);
        Array.Copy(tempPng, bufferStartInPng, typeAndData, 4, totalBufferSize);
        uint crc = CalculateCrc32(typeAndData);
        byte[] crcBytes = BitConverter.GetBytes(crc).Reverse().ToArray();
        Array.Copy(crcBytes, 0, tempPng, crcPosition, 4);

        return tempPng;
    }

    private static byte[] BuildChunk(string chunkType, byte[] data)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Length (big-endian)
        writer.Write(BitConverter.GetBytes(data.Length).Reverse().ToArray());

        // Type
        writer.Write(Encoding.ASCII.GetBytes(chunkType));

        // Data
        writer.Write(data);

        // CRC32 (calculated over type + data)
        byte[] typeAndData = new byte[4 + data.Length];
        Array.Copy(Encoding.ASCII.GetBytes(chunkType), 0, typeAndData, 0, 4);
        Array.Copy(data, 0, typeAndData, 4, data.Length);
        uint crc = CalculateCrc32(typeAndData);
        writer.Write(BitConverter.GetBytes(crc).Reverse().ToArray());

        return ms.ToArray();
    }

    private byte[] ManuallyDecodeBase64(string text)
    {
        // Convert each character to its 6-bit value
        var bits = new List<bool>();
        foreach (char c in text)
        {
            if (c == _mode.PaddingChar || char.IsWhiteSpace(c)) continue; // Skip padding and spaces

            int value = _mode.Chars.IndexOf(c);
            if (value == -1)
            {
                // Invalid base64 character, skip or use 0
                value = 0;
            }

            // Add 6 bits
            for (int i = 5; i >= 0; i--)
            {
                bits.Add((value & (1 << i)) != 0);
            }
        }

        // Convert bits to bytes (8 bits per byte)
        var bytes = new List<byte>();
        for (int i = 0; i + 7 < bits.Count; i += 8)
        {
            byte b = 0;
            for (int j = 0; j < 8; j++)
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

    private static uint CalculateCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320;
                else
                    crc >>= 1;
            }
        }
        return ~crc;
    }
}
