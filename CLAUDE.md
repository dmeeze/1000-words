# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Application that embeds text into PNG files such that the text appears verbatim in the base64-encoded output with proper line layout. Text is displayed at standard 76-character base64 line boundaries, with each input line appearing on its own line in the base64 output. For example, entering "CLI Test\nMessage" will produce a modified PNG where each line appears at the start of a base64 line.

### Projects

- **Llm64.Wasm** - Blazor WebAssembly application with web UI (minimal Blazor, mostly static HTML)
- **Llm64.Cli** - Command-line interface
- **Llm64.Tests** - Unit tests

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run Blazor WASM app
dotnet run --project Llm64.Wasm/Llm64.Wasm.csproj
dotnet watch --project Llm64.Wasm/Llm64.Wasm.csproj  # with hot reload

# Run CLI
dotnet run --project Llm64.Cli/Llm64.Cli.csproj -- -i input.png -o output.png -m "message"

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestName"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Architecture

### Core Logic: PngTextEmbedder Service (`/Llm64.Wasm/Services/PngTextEmbedder.cs`)

Static utility class that handles all PNG manipulation logic. Key methods:

1. **`NormalizePrompt(string prompt)`** - Normalizes user input and returns `List<string>`:
   - Preserves newlines as separate list entries (multi-line support)
   - Spaces → `+` within each line
   - Filters to valid base64 chars only (A-Z, a-z, 0-9, +, /, =)
   - Returns list of normalized lines (empty lines preserved)

2. **`ProcessLinesForEmbedding(List<string> lines)`** - Processes normalized lines for embedding:
   - Pads each line to exactly 76 characters (standard base64 line width)
   - Wraps lines longer than 76 characters to multiple lines
   - Empty lines become full lines of `+` padding
   - Returns list of 76-character strings ready for embedding

3. **`EmbedTextInPng(byte[] pngData, string text)`** - Main embedding logic:
   - Validates PNG signature
   - Finds IHDR chunk position
   - Normalizes input text and processes lines for 76-char layout
   - Calls `EmbedTextWithOverwrite()` (insert empty buffer, then overwrite)
   - Inserts custom "sLOp" ancillary chunk after IHDR
   - Returns modified PNG bytes

4. **`ManuallyDecodeBase64(string text)`** - Custom base64 decoder:
   - Converts each base64 char to 6 bits
   - Packs bits into bytes (8 bits per byte)
   - **Why manual?** Standard `Convert.FromBase64String()` with `=` padding corrupts the last byte during round-trip encoding

### PNG Chunk Structure

Uses custom ancillary chunk type **"sLOP"** (lowercase 's' = ancillary, uppercase 'P' = unsafe-to-copy):
- Inserted after IHDR chunk (beginning of file)
- Contains single copy of text at calculated alignment
- Includes CRC32 checksum for PNG validity
- Padded with 0xFB 0xEF 0xBE pattern (encodes to `++++` in base64)
- **Unsafe-to-copy:** The 'P' (uppercase) indicates this chunk cannot be safely copied by PNG editors that don't recognize it, because the exact byte position is critical for 76-char line alignment

### Embedding Implementation Details

**`EmbedTextWithOverwrite()` Method:**
1. Ensures text length is multiple of 4 for proper base64 encoding
2. Decodes text to raw bytes using `ManuallyDecodeBase64()`
3. Calculates padding needed to align to 57-byte boundary (76 base64 chars)
4. Adds one full line (57 bytes) of padding before and after text
5. Creates empty buffer and builds sLOp chunk
6. Inserts chunk into PNG
7. Overwrites buffer with alignment-aware padding + raw bytes
8. Recalculates CRC32 for modified chunk

**57-Byte Boundary Alignment:**
- Standard base64 wraps at 76 characters = 57 bytes (76 ÷ 4 × 3 = 57)
- Text is aligned so each processed line starts at a 57-byte boundary
- This ensures each text line appears at the start of a base64 line in output

**Key Helper Methods:**
- `GeneratePaddingForAlignment(int alignment, int byteCount)`:
  - Uses pattern 0xFB 0xEF 0xBE repeated (encodes to `++++`)
  - Adds null byte prefix to align pattern to 3-byte boundaries
  - Padding appears as `++++` in base64 output at any byte alignment
- `ManuallyDecodeBase64(string text)`:
  - Converts base64 text to raw bytes
  - Standard .NET `Convert.FromBase64String()` corrupts bytes during round-trip when `=` padding is present
- `BuildChunk(string chunkType, byte[] data)`:
  - Creates PNG chunk with proper structure: length + type + data + CRC32

### UI Layer

**Static HTML Pages:**
- `index.html`: Home page with navigation, hero section, and Blazor component container
- `about.html`: Static about/documentation page (no Blazor)
- `css/common.css`: Unified stylesheet (4.9 KB, 270 lines) containing all styles

**Blazor Components (Minimal):**
- `Pages/Index.razor`: Interactive main-card component rendered into `#app` div
  - File upload via `InputFile` component
  - Text input via `textarea`
  - Calls `PngTextEmbedder` service methods
  - Displays modified image with download link
  - Shows base64 excerpt with embedded text highlighted
- `_Imports.razor`: Minimal global using statements (only Forms and Web)
- No routing, no layouts - direct component rendering via `Program.cs`

### CLI Application (`/Llm64.Cli/Program.cs`)

Command-line interface:
- Parses command-line arguments: `-i` (input), `-o` (output), `-m` (message)
- Validates input file exists and is valid PNG
- Calls `PngTextEmbedder.NormalizePrompt()` and `PngTextEmbedder.EmbedTextInPng()`
- Writes output PNG to specified path
- Returns exit code 0 on success, 1 on error

## Text Padding Behavior

### Line Processing
Each input line is normalized and then padded to exactly 76 characters:

Examples:
- `"Hello World"` → `"Hello+World"` → padded to 76 chars
- `"CLI Test\nMessage"` → Two lines: `"CLI+Test"` (padded to 76) and `"Message"` (padded to 76)
- Lines longer than 76 chars are wrapped to multiple 76-char lines
- Empty lines become full lines of `+` padding

### Base64 Alignment
- Each 76-character line encodes to exactly one 76-character base64 line
- Text is aligned to 57-byte boundaries in the PNG (76 ÷ 4 × 3 = 57)
- This ensures each text line appears at the start of a base64 output line

### Example Output
Input: `"CLI Test\nMessage"`

Base64 output (76 chars per line):
```
iVBORw0KGgoAAAANSUhEUgAAAGQAAABkCAIAAAD/gAIDAAAA9HNMT3AA++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
CLI+Test++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
Message+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
```

## Test Coverage (`/Llm64.Tests/PngTextEmbedderTests.cs`)

37 tests covering:
- PNG validation
- Text normalization with line preservation
- Multi-line text embedding
- 76-character line boundary alignment
- Byte alignment verification across different positions
- Base64 embedding correctness
- PNG structure preservation
- Padding patterns (`++++` vs `7777`)
- Edge cases (empty arrays, invalid PNGs, various text lengths)

Key tests:
- `EmbedTextInPng_TextAppearsInBase64_AtCorrectAlignment` verifies that normalized text appears verbatim in base64 output
- `EmbedTextInPng_LinesAppearAt76CharBoundaries` verifies that each text line appears at the start of a 76-char base64 line
- `EmbedTextInPng_WithMultiLineText_WorksCorrectly` verifies multi-line text handling

## Troubleshooting

### Text Not Appearing at Line Boundaries
If embedded text doesn't appear at the start of base64 lines:
1. Verify the chunk is at the correct position (after IHDR)
2. Check that 57-byte boundary alignment is calculated correctly
3. Ensure `ProcessLinesForEmbedding()` is padding lines to exactly 76 characters
4. Verify the sLOP chunk hasn't been moved or modified by other tools

### Text Appears Corrupted
If embedded text appears corrupted in base64 output:
1. Check that `ManuallyDecodeBase64()` is being used (not `Convert.FromBase64String()`)
2. Confirm byte alignment padding is calculated correctly for base64 encoding
3. Verify the PNG hasn't been modified by tools that don't preserve the sLOP chunk
4. Add debug test to verify round-trip: text → decode → encode → verify
