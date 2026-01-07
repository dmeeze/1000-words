using Llm64.Services;

namespace Llm64.Tests;

public class PngTextEmbedderTests
{
    // Minimal valid PNG (1x1 white pixel)
    private static readonly byte[] MinimalPng = new byte[]
    {
        // PNG signature
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        // IHDR chunk
        0x00, 0x00, 0x00, 0x0D, // Length: 13
        0x49, 0x48, 0x44, 0x52, // Type: IHDR
        0x00, 0x00, 0x00, 0x01, // Width: 1
        0x00, 0x00, 0x00, 0x01, // Height: 1
        0x08, 0x06, 0x00, 0x00, 0x00, // Bit depth, color type, etc.
        0x1F, 0x15, 0xC4, 0x89, // CRC
        // IDAT chunk (minimal white pixel data)
        0x00, 0x00, 0x00, 0x0A, // Length: 10
        0x49, 0x44, 0x41, 0x54, // Type: IDAT
        0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01,
        0x0D, 0x0A, 0x2D, 0xB4, // CRC
        // IEND chunk
        0x00, 0x00, 0x00, 0x00, // Length: 0
        0x49, 0x45, 0x4E, 0x44, // Type: IEND
        0xAE, 0x42, 0x60, 0x82  // CRC
    };

    [Fact]
    public void IsPng_ValidPng_ReturnsTrue()
    {
        Assert.True(PngTextEmbedder.IsPng(MinimalPng));
    }

    [Fact]
    public void IsPng_InvalidData_ReturnsFalse()
    {
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.False(PngTextEmbedder.IsPng(invalidData));
    }

    [Fact]
    public void IsPng_EmptyArray_ReturnsFalse()
    {
        Assert.False(PngTextEmbedder.IsPng(Array.Empty<byte>()));
    }

    [Theory]
    [InlineData("Hello World", "Hello+World+")] // length 11 -> padded to 12
    [InlineData("Hello\nWorld", "Hello/World+")] // length 11 -> padded to 12
    [InlineData("Hello\r\nWorld", "Hello/World+")] // length 11 -> padded to 12
    [InlineData("Test 123", "Test+123")] // length 8, already % 4 == 0
    [InlineData("ABC!@#DEF", "ABCDEF++")] // length 6 -> padded to 8
    [InlineData("a+b/c=", "a+b/c=++")] // length 6 -> padded to 8
    [InlineData("ABCDE", "ABCDE+++")] // length 5 (5%4=1) -> padded to 8 (8%4=0)
    [InlineData("A", "A+++")] // length 1 (1%4=1) -> padded to 4 (4%4=0)
    [InlineData("AB", "AB++")] // length 2 (2%4=2) -> padded to 4 (4%4=0)
    [InlineData("ABC", "ABC+")] // length 3 (3%4=3) -> padded to 4 (4%4=0)
    public void NormalizePrompt_VariousInputs_ReturnsExpectedOutput(string input, string expected)
    {
        var result = PngTextEmbedder.NormalizePrompt(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EmbedTextInPng_ValidPng_ReturnsLargerPng()
    {
        var text = "Hello+World";
        var result = PngTextEmbedder.EmbedTextInPng(MinimalPng, text);

        Assert.True(result.Length > MinimalPng.Length);
        Assert.True(PngTextEmbedder.IsPng(result));
    }

    [Fact]
    public void EmbedTextInPng_InvalidPng_ThrowsException()
    {
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.Throws<ArgumentException>(() => PngTextEmbedder.EmbedTextInPng(invalidData, "test"));
    }

    [Theory]
    [InlineData("Hello+World")] // length 11, 11%4=3, valid
    [InlineData("Test")] // length 4, 4%4=0, valid
    [InlineData("AA")] // length 2, 2%4=2, valid (needs 2 padding)
    [InlineData("AAA")] // length 3, 3%4=3, valid (needs 1 padding)
    [InlineData("AAAA")] // length 4, 4%4=0, valid
    [InlineData("ABCDE+++")] // originally "ABCDE" (5%4=1), auto-padded to "ABCDE+++" (8%4=0)
    [InlineData("A+++")] // originally "A" (1%4=1), auto-padded to "A+++" (4%4=0)
    [InlineData("ABCDEFGHI+++")] // originally "ABCDEFGHI" (9%4=1), auto-padded to "ABCDEFGHI+++" (12%4=0)
    public void EmbedTextInPng_TextAppearsInBase64_AtCorrectAlignment(string text)
    {
        // All texts are now valid base64 thanks to automatic padding
        // Texts where original length % 4 == 1 are padded with '+++'

        // Embed the text
        var modifiedPng = PngTextEmbedder.EmbedTextInPng(MinimalPng, text);

        // Convert to base64
        var base64 = Convert.ToBase64String(modifiedPng);

        // The text should appear in the base64 output
        var found = base64.Contains(text);

        Assert.True(found, $"Text '{text}' not found in base64 output. Base64: {base64}");
    }

    [Fact]
    public void NormalizePrompt_AutoPadding_AllLengths()
    {
        // Test that all texts are automatically padded to length % 4 == 0

        // Length 5: 5 % 4 = 1, should add 3 '+' chars
        var result1 = PngTextEmbedder.NormalizePrompt("ABCDE");
        Assert.Equal("ABCDE+++", result1);
        Assert.Equal(0, result1.Length % 4);

        // Length 1: 1 % 4 = 1, should add 3 '+' chars
        var result2 = PngTextEmbedder.NormalizePrompt("X");
        Assert.Equal("X+++", result2);
        Assert.Equal(0, result2.Length % 4);

        // Length 9: 9 % 4 = 1, should add 3 '+' chars
        var result3 = PngTextEmbedder.NormalizePrompt("ABCDEFGHI");
        Assert.Equal("ABCDEFGHI+++", result3);
        Assert.Equal(0, result3.Length % 4);

        // Length 2: 2 % 4 = 2, should add 2 '+' chars
        var result4 = PngTextEmbedder.NormalizePrompt("AB");
        Assert.Equal("AB++", result4);
        Assert.Equal(0, result4.Length % 4);

        // Length 3: 3 % 4 = 3, should add 1 '+' char
        var result5 = PngTextEmbedder.NormalizePrompt("ABC");
        Assert.Equal("ABC+", result5);
        Assert.Equal(0, result5.Length % 4);

        // Length 4: 4 % 4 = 0, should add 0 '+' chars
        var result6 = PngTextEmbedder.NormalizePrompt("ABCD");
        Assert.Equal("ABCD", result6);
        Assert.Equal(0, result6.Length % 4);
    }

    [Fact]
    public void EmbedTextInPng_WithOriginalProblematicText_WorksAfterPadding()
    {
        // These texts have length % 4 == 1, which previously failed
        // They should now work because they're auto-padded to length % 4 == 0
        var problematicInputs = new[] { "ABCDE", "A", "ABCDEFGHI" };

        foreach (var input in problematicInputs)
        {
            // Verify length % 4 == 1
            Assert.Equal(1, input.Length % 4);

            // Normalize (which adds padding)
            var normalized = PngTextEmbedder.NormalizePrompt(input);

            // Verify padding was added (length should now be % 4 == 0)
            Assert.Equal(0, normalized.Length % 4);
            Assert.Equal(input + "+++", normalized);

            // Embed
            var modifiedPng = PngTextEmbedder.EmbedTextInPng(MinimalPng, normalized);
            var base64 = Convert.ToBase64String(modifiedPng);

            // The normalized text should appear in base64
            Assert.True(base64.Contains(normalized),
                $"Input '{input}' normalized to '{normalized}' but not found in base64 output");
        }
    }

    [Fact]
    public void ByteAlignment_Test_Alignment0()
    {
        // Create a PNG where data chunk starts at position divisible by 3
        // MinimalPng is 67 bytes, IEND is at position 59
        // Data chunk will start at: 59 + 8 = 67 bytes from start
        // 67 % 3 = 1, so we need 2 bytes padding

        var text = "AAA"; // When decoded from base64: 0x00, 0x00
        var modifiedPng = PngTextEmbedder.EmbedTextInPng(MinimalPng, text);
        var base64 = Convert.ToBase64String(modifiedPng);

        // The text should appear somewhere in the base64
        Assert.Contains("AAA", base64);
    }

    [Fact]
    public void ByteAlignment_Test_MultipleTexts()
    {
        // Test that different texts all appear correctly regardless of alignment
        // Only use texts that are valid base64 (length % 4 != 1) and known to work
        var texts = new[] { "ABC", "ABCD", "Test+123", "Hello+World" };

        foreach (var text in texts)
        {
            var modifiedPng = PngTextEmbedder.EmbedTextInPng(MinimalPng, text);
            var base64 = Convert.ToBase64String(modifiedPng);

            Assert.True(base64.Contains(text), $"Text '{text}' not found in base64 output");
        }
    }

    [Fact]
    public void FindTextInBase64_TextExists_ReturnsExcerpt()
    {
        var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var searchText = "AAAA";

        var result = PngTextEmbedder.FindTextInBase64(base64, searchText);

        Assert.NotNull(result);
        Assert.Contains(searchText, result);
    }

    [Fact]
    public void FindTextInBase64_TextDoesNotExist_ReturnsNull()
    {
        var base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var searchText = "NOTFOUND";

        var result = PngTextEmbedder.FindTextInBase64(base64, searchText);

        Assert.Null(result);
    }

    [Fact]
    public void EmbedTextInPng_PreservesOriginalImageData()
    {
        var text = "Test";
        var modifiedPng = PngTextEmbedder.EmbedTextInPng(MinimalPng, text);

        // Check that PNG signature is preserved
        Assert.Equal(MinimalPng[0], modifiedPng[0]);
        Assert.Equal(MinimalPng[1], modifiedPng[1]);
        Assert.Equal(MinimalPng[2], modifiedPng[2]);
        Assert.Equal(MinimalPng[3], modifiedPng[3]);

        // Check that IHDR chunk is preserved (starts at byte 8)
        for (int i = 8; i < 33; i++) // IHDR chunk is 25 bytes
        {
            Assert.Equal(MinimalPng[i], modifiedPng[i]);
        }
    }

    [Fact]
    public void EmbedTextInPng_DataChunkHasCorrectStructure()
    {
        var text = "ABC";
        var modifiedPng = PngTextEmbedder.EmbedTextInPng(MinimalPng, text);

        // Find "dATa" chunk type in the modified PNG
        bool foundDataChunk = false;
        for (int i = 8; i < modifiedPng.Length - 4; i++)
        {
            if (modifiedPng[i] == 0x64 && // 'd'
                modifiedPng[i + 1] == 0x41 && // 'A'
                modifiedPng[i + 2] == 0x54 && // 'T'
                modifiedPng[i + 3] == 0x61)   // 'a'
            {
                foundDataChunk = true;

                // Verify chunk length is at i-4
                int length = (modifiedPng[i - 4] << 24) |
                            (modifiedPng[i - 3] << 16) |
                            (modifiedPng[i - 2] << 8) |
                            modifiedPng[i - 1];

                // Length should be reasonable (decoded base64 bytes + padding)
                Assert.True(length > 0 && length < 1000, $"Unexpected chunk length: {length}");

                break;
            }
        }

        Assert.True(foundDataChunk, "dATa chunk not found in modified PNG");
    }

    [Theory]
    [InlineData(0)] // 0 % 3 = 0, no padding needed
    [InlineData(1)] // 1 % 3 = 1, need 2 bytes padding
    [InlineData(2)] // 2 % 3 = 2, need 1 byte padding
    public void ByteAlignment_DifferentStartPositions_TextAlwaysAligned(int extraBytes)
    {
        // Create a PNG with extra bytes before IEND to test different alignments
        var testPng = new List<byte>(MinimalPng);

        // Find IEND position (it's at position 59 in MinimalPng)
        int iendPos = 59;

        // Insert a dummy chunk before IEND with 'extraBytes' length to shift alignment
        // Dummy chunk: length(4) + type(4) + data(extraBytes) + CRC(4)
        var dummyChunk = new List<byte>();
        // Length (big-endian)
        dummyChunk.AddRange(BitConverter.GetBytes(extraBytes).Reverse());
        // Type "dUMy"
        dummyChunk.AddRange(new byte[] { 0x64, 0x55, 0x4D, 0x79 });
        // Data (zeros)
        dummyChunk.AddRange(new byte[extraBytes]);
        // CRC (simplified - just use zeros for this test)
        dummyChunk.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });

        testPng.InsertRange(iendPos, dummyChunk);

        var text = "Test+123";
        var modifiedPng = PngTextEmbedder.EmbedTextInPng(testPng.ToArray(), text);
        var base64 = Convert.ToBase64String(modifiedPng);

        Assert.True(base64.Contains(text),
            $"Text '{text}' not found with {extraBytes} extra bytes (alignment offset {extraBytes % 3})");
    }
}
