# PNG Base64 Embedder

Blazor WASM application that embeds text into PNG files such that the text appears in the base64 encoding with proper line layout at 76-character boundaries.

**Implementation:**
- Service class: `/Services/PngTextEmbedder.cs`
- UI: `/Pages/Index.razor`
- Tests: `/Llm64.Tests/PngTextEmbedderTests.cs` (37 tests passing)

## Features

- Upload PNG files
- Enter text prompt (with support for spaces and multi-line text)
- Text normalization:
  - Spaces → `+`
  - Newlines preserved as separate lines
  - Invalid base64 characters removed
  - Each line padded to exactly 76 characters
- Embeds decoded base64 bytes into PNG metadata aligned to 76-char boundaries
- Maintains PNG validity via custom ancillary chunks with proper CRC32
- Display modified image with download option
- Shows base64 excerpt highlighting embedded text
- Each text line appears at the start of a 76-character base64 line

## How It Works

1. User provides text like "CLI Test\nMessage"
2. Normalizes each line: "CLI+Test" and "Message"
3. Pads each line to exactly 76 characters
4. Decodes the combined text AS base64 to get raw bytes
5. Creates an empty buffer (padding + text + padding) and inserts it into a custom PNG ancillary chunk ("sLOP") positioned after IHDR
6. Calculates the exact position and 57-byte boundary alignment in the final PNG
7. Overwrites the buffer with alignment-aware padding and raw bytes at the correct position
8. Recalculates CRC32 for the modified chunk
9. When the modified PNG is base64 encoded, each text line appears at the start of a 76-character base64 line

## Technical Details

### PNG Chunk Structure

- **Chunk type:** "sLOP"
  - Per PNG spec: lowercase 's' = ancillary chunk, uppercase 'P' = unsafe-to-copy
  - Unsafe-to-copy: The exact byte position is critical for 76-char line alignment
  - Inserted after IHDR chunk (beginning of file, after signature and header)
  - Contains text aligned to 57-byte boundaries (76 base64 chars)

### Implementation Method

1. Normalize input text and split into lines
2. Pad each line to exactly 76 characters
3. Calculate buffer size needed (padding + text + padding)
4. Insert empty buffer (all 0x00) into PNG as sLOP chunk
5. Calculate exact position and 57-byte boundary alignment in final PNG
6. Overwrite buffer with alignment-aware padding + raw bytes
7. Recalculate CRC32 for modified chunk

### Padding

- **Between blocks:** 57 bytes (76 chars in base64)
  - Pattern: 0xFB 0xEF 0xBE repeated (encodes to `++++`)
  - Alignment-aware: adds null byte prefix to reach 3-byte boundary before pattern starts
  - Padding appears as `++++` in base64 output at any byte alignment
  - One full line (57 bytes) of padding before and after text
- **Line padding:** Each text line padded to exactly 76 characters
  - Pads with `+` characters to fill the line
  - Lines longer than 76 chars wrap to multiple lines
  - Examples:
    - "CLI Test" → "CLI+Test" + padding to 76 chars
    - "Message" → "Message" + padding to 76 chars
    - Empty line → full line of `+` padding (76 chars)

## Running

```bash
dotnet run
# or
dotnet watch
```
