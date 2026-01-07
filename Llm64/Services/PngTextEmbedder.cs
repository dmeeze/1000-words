using System.Text;

namespace Llm64.Services;

public class PngTextEmbedder
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static bool IsPng(byte[] data)
    {
        if (data.Length < 8) return false;
        return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
               data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
    }

    public static string NormalizePrompt(string prompt)
    {
        // Replace spaces with +, newlines with /
        string normalized = prompt.Replace("\r\n", "/").Replace("\n", "/").Replace("\r", "/").Replace(" ", "+");

        // Keep only valid base64 characters: A-Z, a-z, 0-9, +, /, =
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=')
            {
                sb.Append(c);
            }
        }

        string result = sb.ToString();

        // Pad with '+' (space) to make length % 4 == 0
        // This avoids issues with '=' padding affecting the last byte during decode/encode
        int mod = result.Length % 4;
        if (mod != 0)
        {
            // Add enough '+' chars to make length % 4 == 0
            result = result + new string('+', 4 - mod);
        }

        return result;
    }

    public static byte[] EmbedTextInPng(byte[] pngData, string text)
    {
        if (!IsPng(pngData))
        {
            throw new ArgumentException("Invalid PNG file", nameof(pngData));
        }

        // Find IEND chunk (should be at the end)
        int iendIndex = FindChunk(pngData, "IEND");
        if (iendIndex == -1)
        {
            throw new Exception("Invalid PNG: IEND chunk not found");
        }

        // Calculate byte alignment - we need to know where our data will start
        // to ensure it aligns properly for base64 encoding (3 bytes -> 4 chars)
        int dataStartPosition = iendIndex;

        // Create custom chunk with our payload, considering byte alignment
        byte[] dataChunk = CreateDataChunk(text, dataStartPosition);

        // Insert the chunk before IEND
        byte[] result = new byte[pngData.Length + dataChunk.Length];
        Array.Copy(pngData, 0, result, 0, iendIndex);
        Array.Copy(dataChunk, 0, result, iendIndex, dataChunk.Length);
        Array.Copy(pngData, iendIndex, result, iendIndex + dataChunk.Length, pngData.Length - iendIndex);

        return result;
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

    private static byte[] CreateDataChunk(string text, int insertPosition)
    {
        // Calculate where our data bytes will actually start in the PNG file
        // Chunk structure: length(4) + type(4) + data(N) + CRC(4)
        // So our data starts at: insertPosition + 4 (length) + 4 (type) = insertPosition + 8
        int actualDataPosition = insertPosition + 8;

        // Base64 encodes 3 bytes into 4 characters
        // We need to ensure our data aligns on a 3-byte boundary for the text to appear correctly
        int alignment = actualDataPosition % 3;
        int paddingNeeded = alignment == 0 ? 0 : (3 - alignment);

        // The actual data will start at this aligned position
        int alignedPosition = actualDataPosition + paddingNeeded;

        // Now we need to compute what bytes will encode to our target text
        // Base64: 3 bytes -> 4 chars, so we need (text.length * 3 / 4) bytes
        byte[] rawBytes = DecodeBase64ForAlignment(text, alignedPosition % 3);

        // Prepend padding bytes if needed
        byte[] alignedData = new byte[paddingNeeded + rawBytes.Length];
        // Use null bytes for padding
        Array.Copy(rawBytes, 0, alignedData, paddingNeeded, rawBytes.Length);

        // Use a custom ancillary chunk "dATa" to embed arbitrary bytes
        // Lowercase first letter = ancillary chunk (safe to copy, can be ignored by readers)
        string chunkType = "dATa";

        // Build complete chunk: length + type + data + CRC
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Length (big-endian)
        writer.Write(BitConverter.GetBytes(alignedData.Length).Reverse().ToArray());

        // Type
        writer.Write(Encoding.ASCII.GetBytes(chunkType));

        // Data (raw bytes with padding)
        writer.Write(alignedData);

        // CRC32 (calculated over type + data)
        byte[] typeAndData = new byte[4 + alignedData.Length];
        Array.Copy(Encoding.ASCII.GetBytes(chunkType), 0, typeAndData, 0, 4);
        Array.Copy(alignedData, 0, typeAndData, 4, alignedData.Length);
        uint crc = CalculateCrc32(typeAndData);
        writer.Write(BitConverter.GetBytes(crc).Reverse().ToArray());

        return ms.ToArray();
    }

    private static byte[] DecodeBase64ForAlignment(string text, int alignment)
    {
        // Since we're inserting with proper alignment (alignment should be 0),
        // we can decode directly. However, we need to handle the case where
        // the text doesn't decode perfectly.

        // Try to decode as valid base64
        // Add padding if needed
        string paddedText = text;
        int mod = text.Length % 4;
        if (mod != 0)
        {
            paddedText = text + new string('=', 4 - mod);
        }

        try
        {
            // Attempt to decode
            byte[] decoded = Convert.FromBase64String(paddedText);

            // Verify by encoding back and checking if it matches
            string reencoded = Convert.ToBase64String(decoded);

            // Check if the target text appears in the re-encoded version
            if (reencoded.StartsWith(text) || reencoded.Contains(text))
            {
                return decoded;
            }

            // If not, we need to manually construct the bytes
            return ManuallyDecodeBase64(text);
        }
        catch
        {
            // Decoding failed, manually construct bytes
            return ManuallyDecodeBase64(text);
        }
    }

    private static byte[] ManuallyDecodeBase64(string text)
    {
        // Base64 alphabet
        const string base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        // Convert each character to its 6-bit value
        var bits = new List<bool>();
        foreach (char c in text)
        {
            if (c == '=' || c == ' ') continue; // Skip padding and spaces

            int value = base64Chars.IndexOf(c);
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
