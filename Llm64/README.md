# PNG Base64 Embedder

## Status: Complete ✓

Blazor WASM application that embeds text into PNG files such that the text appears in the base64 encoding.

**Implementation:**
- Service class: `/Services/PngTextEmbedder.cs`
- UI: `/Pages/Index.razor`
- Tests: `/Llm64.Tests/PngTextEmbedderTests.cs` (34 tests passing)

## Features

- Upload PNG files
- Enter text prompt (with support for spaces and newlines)
- Text normalization:
  - Spaces → `+`
  - Newlines → `/`
  - Invalid base64 characters removed
- Embeds decoded base64 bytes into PNG metadata
- Maintains PNG validity via custom ancillary chunks with proper CRC32
- Display modified image with download option
- Shows base64 excerpt highlighting embedded text

## How It Works

1. User provides text like "Hello World"
2. Normalizes to "Hello+World"
3. Decodes "Hello+World" AS base64 to get raw bytes (e.g., `1d e9 65 a3 e5 a8 ae 57 7e`)
4. Embeds those bytes into a custom PNG ancillary chunk ("dATa")
5. When the modified PNG is base64 encoded, those bytes encode back to "Hello+World"

## Technical Details

- Uses a custom ancillary chunk type "dATa" (lowercase first letter = safe-to-copy)
- This allows embedding arbitrary binary data while maintaining PNG validity
- tEXt chunks are limited to Latin-1 text, so we use a custom chunk for raw bytes
- Byte alignment: Data is padded to ensure correct base64 encoding alignment (3 bytes -> 4 chars)
- Auto-padding: All texts are padded with `+` (space) characters to length % 4 == 0
  - This ensures clean base64 encoding/decoding without `=` padding issues
  - Examples:
    - "Hello World" → "Hello+World+" (11 → 12 chars)
    - "ABCDE" → "ABCDE+++" (5 → 8 chars)
    - "Test" → "Test" (already 4 chars, no padding needed)

## Running

```bash
dotnet run
# or
dotnet watch
```
