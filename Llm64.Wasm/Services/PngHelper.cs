using System.Text;

namespace Llm64.Wasm.Services;

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

public class PngHelper
{
    public PngHelper(Base64Mode? mode = null)
    {
        _base64Helper = new Base64Helper(mode);
    }

    private readonly Base64Helper _base64Helper;
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private const string PngHeaderChunkType = "IHDR";
    
    public bool IsPng(byte[] data) => (data.Length >= PngSignature.Length) && PngSignature.SequenceEqual(data.Take(PngSignature.Length));

    public (int width, int height) GetImageDimensions(byte[] pngData)
    {
        // PNG IHDR chunk starts at byte 16 (after signature and IHDR chunk header)
        // Width is 4 bytes at offset 16, height is 4 bytes at offset 20
        if (pngData.Length < 24)
            return (0, 0);

        int width = (pngData[16] << 24) | (pngData[17] << 16) | (pngData[18] << 8) | pngData[19];
        int height = (pngData[20] << 24) | (pngData[21] << 16) | (pngData[22] << 8) | pngData[23];

        return (width, height);
    }


    private string FormatLinesForEmail(List<string> lines)
    {
        var sb = new StringBuilder();
        var empty = new string(_base64Helper.Mode.WhitespaceChar, Base64Helper.StandardLineWidth);

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
            else if (line.Length <= Base64Helper.StandardLineWidth)
            {
                sb.Append(line.PadRight(Base64Helper.StandardLineWidth, _base64Helper.Mode.WhitespaceChar));
            }
            else
            {
                // poor-man's wrap (maybe we do word breaks one day)
                for (var i = 0; i < line.Length; i += Base64Helper.StandardLineWidth)
                {
                    var length = Math.Min(Base64Helper.StandardLineWidth, line.Length - i);
                    var segment = line.Substring(i, length);
                    sb.Append(segment.PadRight(Base64Helper.StandardLineWidth, _base64Helper.Mode.WhitespaceChar));
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
                sb.Append(_base64Helper.Mode.LinebreakChar);
            }
            else
            {
                // Pad to 76 chars
                sb.Append(line);
                sb.Append(_base64Helper.Mode.LinebreakChar);
            }
        }

        return sb.ToString();
    }

    public string? ExtractEmbeddedText(byte[] pngData)
    {
        if (!IsPng(pngData)) return null;

        // Find sLOP chunk
        var slopIndex = FindChunk(pngData, "sLOP");
        if (slopIndex == -1) return null; // No embedded text found

        // Read chunk length (4 bytes, big-endian)
        var chunkLength = (pngData[slopIndex] << 24) | (pngData[slopIndex + 1] << 16) |
                          (pngData[slopIndex + 2] << 8) | pngData[slopIndex + 3];

        var bufferStartInPng = slopIndex + 8; // After length and type fields

        // Try both Email and Compact styles to detect which was used
        // We'll detect Email style by checking if there are multiple empty lines (padding lines)

        // First, try Email style with 57-byte boundary padding
        var emailPaddingToLineStart = (57 - (bufferStartInPng % 57)) % 57;
        var emailDataStart = bufferStartInPng + emailPaddingToLineStart;
        var emailTextBytesLength = chunkLength - emailPaddingToLineStart;

        // Then, try Compact style with 3-byte boundary padding
        var compactPaddingToLineStart = (3 - (bufferStartInPng % 3)) % 3;
        var compactDataStart = bufferStartInPng + compactPaddingToLineStart;
        var compactTextBytesLength = chunkLength - compactPaddingToLineStart;

        // Extract with Email style first
        byte[] textBytes;

        if (emailTextBytesLength > 0)
        {
            var emailTextBytes = new byte[emailTextBytesLength];
            Array.Copy(pngData, emailDataStart, emailTextBytes, 0, emailTextBytesLength);
            var emailBase64 = Convert.ToBase64String(emailTextBytes);
            var emailLines = new List<string>();
            for (var i = 0; i < emailBase64.Length; i += Base64Helper.StandardLineWidth)
            {
                var length = Math.Min(Base64Helper.StandardLineWidth, emailBase64.Length - i);
                emailLines.Add(emailBase64.Substring(i, length));
            }
            var emailTrimmedLines = emailLines.Select(line => line.TrimEnd(_base64Helper.Mode.WhitespaceChar)).ToList();

            // Check if we have multiple empty lines at the start (Email style signature)
            var emptyLineCount = emailTrimmedLines.TakeWhile(line => string.IsNullOrEmpty(line)).Count();
            if (emptyLineCount >= 2)
            {
                textBytes = emailTextBytes;
            }
            else
            {
                // Use Compact style
                textBytes = new byte[compactTextBytesLength];
                Array.Copy(pngData, compactDataStart, textBytes, 0, compactTextBytesLength);
            }
        }
        else
        {
            // Use Compact style
            textBytes = new byte[compactTextBytesLength];
            Array.Copy(pngData, compactDataStart, textBytes, 0, compactTextBytesLength);
        }

        // Convert to base64 and format at 76 chars per line
        var base64 = Convert.ToBase64String(textBytes);
        var lines = new List<string>();

        for (var i = 0; i < base64.Length; i += Base64Helper.StandardLineWidth)
        {
            var length = Math.Min(Base64Helper.StandardLineWidth, base64.Length - i);
            lines.Add(base64.Substring(i, length));
        }

        // Trim padding characters from the right of each line
        var trimmedLines = lines.Select(line => line.TrimEnd(_base64Helper.Mode.WhitespaceChar)).ToList();

        // Skip leading empty lines (padding header)
        while (trimmedLines.Count > 0 && string.IsNullOrEmpty(trimmedLines[0]))
        {
            trimmedLines.RemoveAt(0);
        }

        // Skip trailing empty lines (padding footer)
        while (trimmedLines.Count > 0 && string.IsNullOrEmpty(trimmedLines[^1]))
        {
            trimmedLines.RemoveAt(trimmedLines.Count - 1);
        }

        if (trimmedLines.Count == 0) return null; // No content found

        // Decode the encoded characters back to original:
        // - WhitespaceChar (+) → space
        // - LinebreakChar (/) → newline
        var decodedLines = trimmedLines.Select(line =>
        {
            var decoded = line.Replace(_base64Helper.Mode.WhitespaceChar, ' ');
            decoded = decoded.Replace(_base64Helper.Mode.LinebreakChar, '\n');
            return decoded;
        }).ToList();

        // Join lines with newlines
        var result = string.Join("\n", decodedLines);

        // Remove exactly one trailing newline (Compact style adds a trailing LinebreakChar)
        // Don't use TrimEnd as that removes ALL trailing newlines
        if (result.EndsWith('\n'))
        {
            result = result.Substring(0, result.Length - 1);
        }

        return result;
    }

    public byte[] EmbedTextInPng(byte[] pngData, string text, EmbeddingStyle style)
    {
        if (!IsPng(pngData)) throw new ArgumentException("Invalid PNG file", nameof(pngData));

        // Remove all existing sLOP chunks before adding new one
        pngData = RemoveAllChunks(pngData, "sLOP");

        // Find IHDR chunk
        var ihdrIndex = FindChunk(pngData, PngHeaderChunkType);
        if (ihdrIndex == -1) throw new Exception("Invalid PNG: Header chunk not found");

        // IHDR chunk structure: length(4) + type(4) + data(13) + CRC(4) = 25 bytes
        var insertPosition = ihdrIndex + 25;

        // Normalize and process lines for proper base64 layout
        var normalizedLines = _base64Helper.NormalizeText(text);
        var embeddedText = style switch
        {
            EmbeddingStyle.Email => FormatLinesForEmail(normalizedLines),
            EmbeddingStyle.Compact => FormatLinesForCompact(normalizedLines),
            _ => throw new InvalidOperationException()
        };
        // Insert empty buffer, then overwrite with correct bytes
        return InsertChunk(pngData, embeddedText, insertPosition, style);
    }

    private static int FindChunk(byte[] data, string chunkType)
    {
        var typeBytes = Encoding.ASCII.GetBytes(chunkType);
        for (var i = 8; i < data.Length - 4; i++)
        {
            if (data[i] == typeBytes[0] && data[i + 1] == typeBytes[1] &&
                data[i + 2] == typeBytes[2] && data[i + 3] == typeBytes[3])
            {
                return i - 4; // Return position of length field
            }
        }
        return -1;
    }

    private static byte[] RemoveAllChunks(byte[] data, string chunkType)
    {
        var result = data;

        // Keep removing chunks until none are found
        while (true)
        {
            var chunkIndex = FindChunk(result, chunkType);
            if (chunkIndex == -1) break; // No more chunks of this type

            // Read chunk length (4 bytes, big-endian)
            var chunkLength = (result[chunkIndex] << 24) | (result[chunkIndex + 1] << 16) |
                              (result[chunkIndex + 2] << 8) | result[chunkIndex + 3];

            // Chunk structure: length(4) + type(4) + data(chunkLength) + CRC(4)
            var totalChunkSize = 4 + 4 + chunkLength + 4;

            // Remove this chunk
            var newData = new byte[result.Length - totalChunkSize];
            Array.Copy(result, 0, newData, 0, chunkIndex);
            Array.Copy(result, chunkIndex + totalChunkSize, newData, chunkIndex, result.Length - (chunkIndex + totalChunkSize));
            result = newData;
        }

        return result;
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
        var remainingBytes = finalByteSize - result.Count;
        
        for (var i = 0; i < remainingBytes; i += 3)
        {
            var copyLen = Math.Min(3, remainingBytes - i);
            for (var j = 0; j < copyLen; j++)
            {
                result.Add(pattern[j]);
            }
        }

        return result.ToArray();
    }

    private byte[] InsertChunk(byte[] pngData, string text, int insertPosition, EmbeddingStyle style)
    {
        // Ensure text length is a multiple of 4 for proper base64 encoding
        var mod = text.Length % 4;
        if (mod != 0)
        {
            text += new string(_base64Helper.Mode.WhitespaceChar, 4 - mod);
        }

        // Decode text to raw bytes
        var rawBytes = _base64Helper.ManuallyDecode(text);

        // Calculate where the buffer will start in the PNG
        // Chunk structure: length(4) + type(4) + data(N) + CRC(4)
        var bufferStartInPng = insertPosition + 8; // After length and type fields

        // Calculate padding needed based on style:
        // - Email style: align to 57-byte boundary (76 base64 chars = 57 bytes)
        //   This makes text start on a new line in email-style base64
        // - Compact style: align to 3-byte boundary only
        //   This makes text appear verbatim but not necessarily at line starts
        var paddingToLineStart = 0;
        if (style == EmbeddingStyle.Email)
        {
            paddingToLineStart = (57 - (bufferStartInPng % 57)) % 57;
        }
        else
        {
            // For Compact style, just align to 3-byte boundary
            paddingToLineStart = (3 - (bufferStartInPng % 3)) % 3;
        }

        // Calculate total buffer size
        var totalBufferSize = paddingToLineStart + rawBytes.Length;

        // Create empty buffer (all zeros)
        var emptyBuffer = new byte[totalBufferSize];

        // Build the chunk with empty buffer
        var chunk = BuildChunk("sLOP", emptyBuffer);

        // Insert the chunk into the PNG
        var tempPng = new byte[pngData.Length + chunk.Length];
        Array.Copy(pngData, 0, tempPng, 0, insertPosition);
        Array.Copy(chunk, 0, tempPng, insertPosition, chunk.Length);
        Array.Copy(pngData, insertPosition, tempPng, insertPosition + chunk.Length, pngData.Length - insertPosition);

        // Now overwrite the buffer with actual content
        var writePosition = bufferStartInPng;

        // Generate padding before text (alignment-aware for base64) - only for Email style
        if (paddingToLineStart > 0)
        {
            var paddingBeforeBytes = GeneratePadding(writePosition % 3, paddingToLineStart);
            Array.Copy(paddingBeforeBytes, 0, tempPng, writePosition, paddingBeforeBytes.Length);
            writePosition += paddingBeforeBytes.Length;
        }

        // Write the raw bytes
        Array.Copy(rawBytes, 0, tempPng, writePosition, rawBytes.Length);
        
        // Recalculate CRC for the modified chunk
        var crcPosition = insertPosition + 8 + totalBufferSize; // After length, type, and data
        var typeAndData = new byte[4 + totalBufferSize];
        Array.Copy(Encoding.ASCII.GetBytes("sLOP"), 0, typeAndData, 0, 4);
        Array.Copy(tempPng, bufferStartInPng, typeAndData, 4, totalBufferSize);
        var crc = CalculateCrc32(typeAndData);
        var crcBytes = BitConverter.GetBytes(crc).Reverse().ToArray();
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
        var typeAndData = new byte[4 + data.Length];
        Array.Copy(Encoding.ASCII.GetBytes(chunkType), 0, typeAndData, 0, 4);
        Array.Copy(data, 0, typeAndData, 4, data.Length);
        var crc = CalculateCrc32(typeAndData);
        writer.Write(BitConverter.GetBytes(crc).Reverse().ToArray());

        return ms.ToArray();
    }

    private static uint CalculateCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
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
