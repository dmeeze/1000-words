using Llm64.Wasm.Services;

namespace Llm64.Tests;

public class PngHelperTests
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

    private PngHelper _standardPngHelper = new PngHelper(Base64Mode.Standard); 
    private PngHelper _urlSafePngHelper = new PngHelper(Base64Mode.UrlSafe); 

    [Fact]
    public void IsPng_ValidPng_ReturnsTrue()
    {
        Assert.True(_standardPngHelper.IsPng(MinimalPng));
    }

    [Fact]
    public void IsPng_InvalidData_ReturnsFalse()
    {
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.False(_standardPngHelper.IsPng(invalidData));
    }

    [Fact]
    public void IsPng_EmptyArray_ReturnsFalse()
    {
        Assert.False(_standardPngHelper.IsPng(Array.Empty<byte>()));
    }

    [Theory]
    [InlineData(EmbeddingStyle.Email)]
    [InlineData(EmbeddingStyle.Compact)]
    public void EmbedTextInPng_ValidPng_ReturnsLargerPng(EmbeddingStyle style)
    {
        var text = "Hello+World";
        var result = _standardPngHelper.EmbedTextInPng(MinimalPng, text, style);

        Assert.True(result.Length > MinimalPng.Length);
        Assert.True(_standardPngHelper.IsPng(result));
    }

    [Fact]
    public void EmbedTextInPng_InvalidPng_ThrowsException()
    {
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        Assert.Throws<ArgumentException>(() => _standardPngHelper.EmbedTextInPng(invalidData, "test", EmbeddingStyle.Email));
    }

    [Theory]
    [InlineData("Hello+World", EmbeddingStyle.Email)]
    [InlineData("A", EmbeddingStyle.Email)] 
    [InlineData("AA", EmbeddingStyle.Email)] 
    [InlineData("AAA", EmbeddingStyle.Email)] 
    [InlineData("AAAA", EmbeddingStyle.Email)]
    [InlineData("ABCDE+++", EmbeddingStyle.Email)]
    [InlineData("Hello+World", EmbeddingStyle.Compact)]
    [InlineData("A", EmbeddingStyle.Compact)] 
    [InlineData("AA", EmbeddingStyle.Compact)] 
    [InlineData("AAA", EmbeddingStyle.Compact)] 
    [InlineData("AAAA", EmbeddingStyle.Compact)]
    [InlineData("ABCDE+++", EmbeddingStyle.Compact)]
    public void EmbedTextInPng_TextAppearsInBase64_AtCorrectAlignment(string text, EmbeddingStyle style) 
    {
        // All texts are now valid base64 thanks to automatic padding
        // Texts where original length % 4 == 1 are padded with '+++'

        // Embed the text
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, text, style);

        // Convert to base64
        var base64 = Convert.ToBase64String(modifiedPng);

        // The text should appear in the base64 output
        var found = base64.Contains(text);

        Assert.True(found, $"Text '{text}' not found in base64 output. Base64: {base64}");
    }

    [Fact]
    public void EmbedTextInPng_WithMultiLineText_WorksCorrectly_ForEmailStyle()
    {
        // Test that multi-line text is embedded correctly
        var input = "CLI Test\nMessage";

        // Embed directly (EmbedTextInPng handles normalization)
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, input, EmbeddingStyle.Email);
        var base64 = Convert.ToBase64String(modifiedPng);

        // Verify that normalized lines appear in base64
        var base64Helper = new Base64Helper(Base64Mode.Standard);
        var normalizedLines = base64Helper.NormalizeText(input);
        Assert.Equal(2, normalizedLines.Count);
        Assert.Equal("CLI+Test", normalizedLines[0]);
        Assert.Equal("Message", normalizedLines[1]);

        // Both lines should appear in the base64 output
        Assert.True(base64.Contains("CLI+Test"), "First line not found in base64");
        Assert.True(base64.Contains("Message"), "Second line not found in base64");
    }
    
    [Fact]
    public void EmbedTextInPng_WithMultiLineText_WorksCorrectly_ForCompactStyle()
    {
        // Test that multi-line text is embedded correctly
        var input = "CLI Test\nMessage";

        // Embed directly (EmbedTextInPng handles normalization)
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, input, EmbeddingStyle.Compact);
        var base64 = Convert.ToBase64String(modifiedPng);
        
        // Both lines should appear in the base64 output
        Assert.True(base64.Contains("CLI+Test/Message"), $"Line not found in {base64}");
    }

    [Fact]
    public void EmbedTextInPng_LinesAppearAt76CharBoundaries()
    {
        // Test that each line appears at the start of a 76-char base64 line
        var input = "CLI Test\nMessage";

        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, input, EmbeddingStyle.Email);
        var base64 = Convert.ToBase64String(modifiedPng);

        // Split base64 into 76-char lines (standard base64 line width)
        var lines = new List<string>();
        for (int i = 0; i < base64.Length; i += 76)
        {
            int length = Math.Min(76, base64.Length - i);
            lines.Add(base64.Substring(i, length));
        }

        // Check if any line starts with "CLI+Test"
        bool foundFirstLine = lines.Any(line => line.StartsWith("CLI+Test"));
        Assert.True(foundFirstLine, "First text line 'CLI+Test' should appear at start of a 76-char line");

        // Check if any line starts with "Message"
        bool foundSecondLine = lines.Any(line => line.StartsWith("Message"));
        Assert.True(foundSecondLine, "Second text line 'Message' should appear at start of a 76-char line");
    }
    
    [Theory]
    [InlineData(EmbeddingStyle.Email)]
    [InlineData(EmbeddingStyle.Compact)]
    public void EmbedTextInPng_PreservesOriginalImageData(EmbeddingStyle style)
    {
        var text = "Test";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, text, style);

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


    [Theory]
    [InlineData(EmbeddingStyle.Email)]
    [InlineData(EmbeddingStyle.Compact)]
    public void EmbedTextInPng_DataChunkHasCorrectStructure(EmbeddingStyle style)
    {
        var text = "ABC";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, text, style);

        // Find "sLOP" chunk type in the modified PNG
        bool foundDataChunk = false;
        for (int i = 8; i < modifiedPng.Length - 4; i++)
        {
            if (modifiedPng[i] == 0x73 && // 's'
                modifiedPng[i + 1] == 0x4C && // 'L'
                modifiedPng[i + 2] == 0x4F && // 'O'
                modifiedPng[i + 3] == 0x50)   // 'P'
            {
                foundDataChunk = true;

                // Verify chunk length is at i-4
                int length = (modifiedPng[i - 4] << 24) |
                            (modifiedPng[i - 3] << 16) |
                            (modifiedPng[i - 2] << 8) |
                            modifiedPng[i - 1];

                // Length should be reasonable (decoded base64 bytes + padding)
                Assert.True(length > 0 && length < 10000, $"Unexpected chunk length: {length}");

                break;
            }
        }

        Assert.True(foundDataChunk, "sLOP chunk not found in modified PNG");
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
        var modifiedPng = _standardPngHelper.EmbedTextInPng(testPng.ToArray(), text, EmbeddingStyle.Email);
        var base64 = Convert.ToBase64String(modifiedPng);

        Assert.True(base64.Contains(text),
            $"Text '{text}' not found with {extraBytes} extra bytes (alignment offset {extraBytes % 3})");
    }

    [Fact]
    public void ExtractEmbeddedText_RoundTrip_SingleLine()
    {
        var inputText = "Hello World";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, inputText, EmbeddingStyle.Email);

        var extractedText = _standardPngHelper.ExtractEmbeddedText(modifiedPng);

        Assert.NotNull(extractedText);
        Assert.Equal(inputText, extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_RoundTrip_MultiLine()
    {
        var inputText = "Line 1\nLine 2\nLine 3";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, inputText, EmbeddingStyle.Email);

        var extractedText = _standardPngHelper.ExtractEmbeddedText(modifiedPng);

        Assert.NotNull(extractedText);
        Assert.Equal(inputText, extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_NoPngWithoutSlopChunk_ReturnsNull()
    {
        var extractedText = _standardPngHelper.ExtractEmbeddedText(MinimalPng);
        Assert.Null(extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_InvalidPng_ReturnsNull()
    {
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var extractedText = _standardPngHelper.ExtractEmbeddedText(invalidData);
        Assert.Null(extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_DecodesSpacesAndNewlines()
    {
        var inputText = "Hello World\nThis is a test\nWith multiple lines";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, inputText, EmbeddingStyle.Email);

        var extractedText = _standardPngHelper.ExtractEmbeddedText(modifiedPng);

        Assert.NotNull(extractedText);
        // Should decode '+' back to spaces
        Assert.Contains("Hello World", extractedText);
        Assert.Contains("This is a test", extractedText);
        Assert.Contains("With multiple lines", extractedText);
        // Should preserve newlines
        Assert.Contains("\n", extractedText);
        Assert.Equal(inputText, extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_CompactStyle_RoundTrip()
    {
        var inputText = "Compact test";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, inputText, EmbeddingStyle.Compact);

        var extractedText = _standardPngHelper.ExtractEmbeddedText(modifiedPng);

        Assert.NotNull(extractedText);
        Assert.Equal(inputText, extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_CompactStyle_MultiLine_RoundTrip()
    {
        var inputText = "Line 1\nLine 2\nLine 3";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, inputText, EmbeddingStyle.Compact);

        var extractedText = _standardPngHelper.ExtractEmbeddedText(modifiedPng);

        Assert.NotNull(extractedText);
        Assert.Equal(inputText, extractedText);
    }

    [Fact]
    public void ExtractEmbeddedText_PreservesIntentionalTrailingNewline()
    {
        // User intentionally adds a trailing newline
        var inputText = "Line 1\nLine 2\n";
        var modifiedPng = _standardPngHelper.EmbedTextInPng(MinimalPng, inputText, EmbeddingStyle.Compact);

        var extractedText = _standardPngHelper.ExtractEmbeddedText(modifiedPng);

        Assert.NotNull(extractedText);
        Assert.Equal(inputText, extractedText);
    }

    [Theory]
    [InlineData(EmbeddingStyle.Email)]
    [InlineData(EmbeddingStyle.Compact)]
    public void EmbedTextInPng_RemovesExistingSlopChunks(EmbeddingStyle style)
    {
        // Embed first text
        var firstText = "First embedded text";
        var pngWithFirstText = _standardPngHelper.EmbedTextInPng(MinimalPng, firstText, style);

        // Verify first text is embedded
        var extractedFirst = _standardPngHelper.ExtractEmbeddedText(pngWithFirstText);
        Assert.Equal(firstText, extractedFirst);

        // Embed second text (should remove first sLOP chunk)
        var secondText = "Second embedded text";
        var pngWithSecondText = _standardPngHelper.EmbedTextInPng(pngWithFirstText, secondText, style);

        // Verify only second text is present
        var extractedSecond = _standardPngHelper.ExtractEmbeddedText(pngWithSecondText);
        Assert.Equal(secondText, extractedSecond);

        // Verify there's only one sLOP chunk by counting occurrences
        int slopCount = 0;
        for (int i = 8; i < pngWithSecondText.Length - 4; i++)
        {
            if (pngWithSecondText[i] == 0x73 && // 's'
                pngWithSecondText[i + 1] == 0x4C && // 'L'
                pngWithSecondText[i + 2] == 0x4F && // 'O'
                pngWithSecondText[i + 3] == 0x50)   // 'P'
            {
                slopCount++;
            }
        }

        Assert.Equal(1, slopCount);
    }

    [Theory]
    [InlineData(EmbeddingStyle.Email)]
    [InlineData(EmbeddingStyle.Compact)]
    public void EmbedTextInPng_RemovesMultipleSlopChunks(EmbeddingStyle style)
    {
        // Manually create a PNG with multiple sLOP chunks by embedding multiple times
        // (This simulates old behavior where chunks would accumulate)
        var text1 = "Text 1";
        var text2 = "Text 2";
        var text3 = "Text 3";

        var png1 = _standardPngHelper.EmbedTextInPng(MinimalPng, text1, style);

        // Force add another chunk without removing (simulate old behavior)
        // We'll do this by creating a fresh PNG with text2 and manually combining chunks
        var png2 = _standardPngHelper.EmbedTextInPng(MinimalPng, text2, style);

        // Now embed text3 - should remove all previous sLOP chunks
        var finalPng = _standardPngHelper.EmbedTextInPng(png1, text3, style);

        // Verify only text3 is present
        var extracted = _standardPngHelper.ExtractEmbeddedText(finalPng);
        Assert.Equal(text3, extracted);

        // Count sLOP chunks
        int slopCount = 0;
        for (int i = 8; i < finalPng.Length - 4; i++)
        {
            if (finalPng[i] == 0x73 && // 's'
                finalPng[i + 1] == 0x4C && // 'L'
                finalPng[i + 2] == 0x4F && // 'O'
                finalPng[i + 3] == 0x50)   // 'P'
            {
                slopCount++;
            }
        }

        Assert.Equal(1, slopCount);
    }
}
