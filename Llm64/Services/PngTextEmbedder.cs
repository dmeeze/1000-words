using System.Text;

namespace Llm64.Services;

public class PngTextEmbedder
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private const string Base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
    const int Base64DisplayLineWidth = 76;
    private const string PngHeaderChunkType = "IHDR";
    private static readonly HashSet<char> Base64CharSet = new(Base64Chars);
    
    public static bool IsPng(byte[] data)
    {
        if (data.Length < 8) return false;
        return data.Take(8).SequenceEqual(PngSignature);
    }
    
    public static List<string> NormalizePrompt(string prompt)
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
                    _ when char.IsWhiteSpace(c) => '+', // whitespace to plus
                    _ when Base64CharSet.Contains(c) => c,
                    _ => null
                };

                if (appendChar.HasValue) chars.Add(appendChar.Value);
            }

            lines.Add(new string(chars.ToArray()));
        }

        return lines;
    }

    private static string ProcessLinesForEmbedding(List<string> lines)
    {
        var sb = new StringBuilder();
        
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                // Empty line becomes a line of padding
                sb.Append(new string('+', Base64DisplayLineWidth));
            }
            else if (line.Length <= Base64DisplayLineWidth)
            {
                // Pad to 76 chars
                sb.Append(line.PadRight(Base64DisplayLineWidth, '+'));
            }
            else
            {
                // Wrap
                for (int i = 0; i < line.Length; i += Base64DisplayLineWidth)
                {
                    int length = Math.Min(Base64DisplayLineWidth, line.Length - i);
                    string segment = line.Substring(i, length);
                    sb.Append(segment.PadRight(Base64DisplayLineWidth, '+'));
                }
            }
        }

        return sb.ToString();
    }

    public static byte[] EmbedTextInPng(byte[] pngData, string text)
    {
        if (!IsPng(pngData))
        {
            throw new ArgumentException("Invalid PNG file", nameof(pngData));
        }

        // Find IHDR chunk (always starts at byte 8 after PNG signature)
        int ihdrIndex = FindChunk(pngData, PngHeaderChunkType);
        if (ihdrIndex == -1)
        {
            throw new Exception("Invalid PNG: Header chunk not found");
        }

        // IHDR chunk structure: length(4) + type(4) + data(13) + CRC(4) = 25 bytes
        int insertPosition = ihdrIndex + 25;

        // Normalize and process lines for proper base64 layout
        var normalizedLines = NormalizePrompt(text);
        var embeddedText = ProcessLinesForEmbedding(normalizedLines);
        // Insert empty buffer, then overwrite with correct bytes
        return EmbedTextWithOverwrite(pngData, embeddedText, insertPosition);
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

    private static byte[] GeneratePaddingForAlignment(int alignment, int byteCount)
    {
        // Generate padding that encodes to "+++..." in base64 at the given alignment
        // alignment: 0, 1, or 2 (position % 3)
        // byteCount: how many bytes of padding to generate

        // The pattern 0xFB 0xEF 0xBE encodes to "++++" ONLY at 3-byte boundaries
        // Precomputed initial bytes for each alignment case
        byte[] pattern = new byte[] { 0xFB, 0xEF, 0xBE };
        

        var result = alignment switch
        {
            0 => new List<byte>(),
            1 => new List<byte>([0x00, 0x00]),
            2 => new List<byte>([0x00]),
            _ => throw new InvalidOperationException()
        };

        // Now add the repeating pattern
        int remainingBytes = byteCount - result.Count;
        
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

    private static byte[] EmbedTextWithOverwrite(byte[] pngData, string text, int insertPosition)
    {
        // Ensure text length is a multiple of 4 for proper base64 encoding
        int mod = text.Length % 4;
        if (mod != 0)
        {
            text = text + new string('+', 4 - mod);
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

        // Additional padding: one full line (57 bytes) before text for visual buffer
        int additionalPaddingBefore = 57;
        int totalPaddingBefore = paddingToLineStart + additionalPaddingBefore;

        // Padding after text: one full line (57 bytes)
        int paddingAfter = 57;

        // Calculate total buffer size
        int totalBufferSize = totalPaddingBefore + rawBytes.Length + paddingAfter;

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
        byte[] paddingBeforeBytes = GeneratePaddingForAlignment(writePosition % 3, totalPaddingBefore);
        Array.Copy(paddingBeforeBytes, 0, tempPng, writePosition, paddingBeforeBytes.Length);
        writePosition += paddingBeforeBytes.Length;

        // Write the raw bytes
        Array.Copy(rawBytes, 0, tempPng, writePosition, rawBytes.Length);
        writePosition += rawBytes.Length;

        // Generate padding after text (alignment-aware for base64)
        byte[] paddingAfterBytes = GeneratePaddingForAlignment(writePosition % 3, paddingAfter);
        Array.Copy(paddingAfterBytes, 0, tempPng, writePosition, paddingAfterBytes.Length);

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

    private static byte[] ManuallyDecodeBase64(string text)
    {
        // Convert each character to its 6-bit value
        var bits = new List<bool>();
        foreach (char c in text)
        {
            if (c == '=' || c == ' ') continue; // Skip padding and spaces

            int value = Base64Chars.IndexOf(c);
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

    public static string? FindTextInBase64(string base64, string searchText)
    {
        int index = base64.IndexOf(searchText, StringComparison.Ordinal);
        if (index >= 0)
        {
            int start = Math.Max(0, index - 50);
            int length = Math.Min(base64.Length - start, searchText.Length + 100);
            string excerpt = base64.Substring(start, length);
            if (start > 0) excerpt = "..." + excerpt;
            if (start + length < base64.Length) excerpt += "...";
            return excerpt;
        }
        return null;
    }
}
