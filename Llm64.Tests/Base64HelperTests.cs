using Llm64.Wasm.Services;

namespace Llm64.Tests;

public class Base64HelperTests
{
    private readonly Base64Helper _standardHelper = new(Base64Mode.Standard);
    private readonly Base64Helper _urlSafeHelper = new(Base64Mode.UrlSafe);

    [Theory]
    [InlineData("Hello World", new[] { "Hello+World" })]
    [InlineData("Hello\nWorld", new[] { "Hello", "World" })]
    [InlineData("Hello\r\nWorld", new[] { "Hello", "World" })]
    [InlineData("Test 123", new[] { "Test+123" })]
    [InlineData("ABC!@#DEF", new[] { "ABCDEF" })]
    [InlineData("a+b/c=", new[] { "a+b/c" })]
    [InlineData("ABCDE", new[] { "ABCDE" })]
    [InlineData("A", new[] { "A" })]
    [InlineData("AB", new[] { "AB" })]
    [InlineData("ABC", new[] { "ABC" })]
    public void NormalizeText_VariousInputs_ReturnsExpectedOutput(string input, string[] expected)
    {
        var result = _standardHelper.NormalizeText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeText_PreservesLinesAndNormalizesChars()
    {
        // Single line
        var result1 = _standardHelper.NormalizeText("ABCDE");
        Assert.Equal(new[] { "ABCDE" }, result1);

        // Multiple lines
        var result2 = _standardHelper.NormalizeText("Line1\nLine2\nLine3");
        Assert.Equal(new[] { "Line1", "Line2", "Line3" }, result2);

        // Spaces become +
        var result3 = _standardHelper.NormalizeText("Hello World");
        Assert.Equal(new[] { "Hello+World" }, result3);

        // Empty lines preserved
        var result4 = _standardHelper.NormalizeText("A\n\nB");
        Assert.Equal(new[] { "A", "", "B" }, result4);
    }

    [Fact]
    public void FormatWithLineBreaks_StandardLineWidth_Formats76CharsPerLine()
    {
        var input = new string('A', 200); // 200 characters
        var result = _standardHelper.FormatWithLineBreaks(input);

        var lines = result.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Should have 3 lines: 76 + 76 + 48
        Assert.Equal(3, lines.Length);
        Assert.Equal(76, lines[0].Length);
        Assert.Equal(76, lines[1].Length);
        Assert.Equal(48, lines[2].Length);
    }

    [Fact]
    public void FormatWithLineBreaks_CustomLineWidth_FormatsCorrectly()
    {
        var input = new string('A', 100);
        var result = _standardHelper.FormatWithLineBreaks(input, lineWidth: 40);

        var lines = result.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Should have 3 lines: 40 + 40 + 20
        Assert.Equal(3, lines.Length);
        Assert.Equal(40, lines[0].Length);
        Assert.Equal(40, lines[1].Length);
        Assert.Equal(20, lines[2].Length);
    }

    [Theory]
    [InlineData("AAAA", new byte[] { 0x00, 0x00, 0x00 })]
    [InlineData("AAA=", new byte[] { 0x00, 0x00 })]
    [InlineData("AA==", new byte[] { 0x00 })]
    public void ManuallyDecode_ValidBase64_DecodesCorrectly(string input, byte[] expected)
    {
        var result = _standardHelper.ManuallyDecode(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ManuallyDecode_IgnoresPaddingAndWhitespace()
    {
        var result1 = _standardHelper.ManuallyDecode("AAAA");
        var result2 = _standardHelper.ManuallyDecode("AAAA====");
        var result3 = _standardHelper.ManuallyDecode("AA AA"); // Whitespace ignored, so this is "AAAA"

        // Padding should be ignored - both should decode the same
        Assert.Equal(result1, result2);

        // Whitespace should be ignored - "AA AA" becomes "AAAA"
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00 }, result3);
        Assert.Equal(result1, result3);
    }

    [Theory]
    [InlineData('A', true)]
    [InlineData('Z', true)]
    [InlineData('a', true)]
    [InlineData('z', true)]
    [InlineData('0', true)]
    [InlineData('9', true)]
    [InlineData('+', true)]
    [InlineData('/', true)]
    [InlineData('!', false)]
    [InlineData('@', false)]
    [InlineData(' ', false)]
    public void IsValidBase64Char_StandardMode_ValidatesCorrectly(char c, bool expected)
    {
        var result = _standardHelper.IsValidBase64Char(c);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Mode_ReturnsCorrectMode()
    {
        Assert.Equal(Base64Mode.Standard, _standardHelper.Mode);
        Assert.Equal(Base64Mode.UrlSafe, _urlSafeHelper.Mode);
    }

    [Fact]
    public void StandardMode_HasCorrectCharacters()
    {
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/", Base64Mode.Standard.Chars);
        Assert.Equal('=', Base64Mode.Standard.PaddingChar);
        Assert.Equal('+', Base64Mode.Standard.WhitespaceChar);
        Assert.Equal('/', Base64Mode.Standard.LinebreakChar);
    }

    [Fact]
    public void UrlSafeMode_HasCorrectCharacters()
    {
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_", Base64Mode.UrlSafe.Chars);
        Assert.Null(Base64Mode.UrlSafe.PaddingChar);
        Assert.Equal('_', Base64Mode.UrlSafe.WhitespaceChar);
        Assert.Equal('-', Base64Mode.UrlSafe.LinebreakChar);
    }
}
