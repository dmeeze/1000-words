# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Blazor WebAssembly application that embeds text into PNG files such that the text appears verbatim in the base64-encoded output. For example, entering "Hello World" will produce a modified PNG whose base64 encoding contains the literal string "Hello+World+".

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run Blazor WASM app (from Llm64/ directory)
dotnet run
dotnet watch  # with hot reload

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestName"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Architecture

### Core Logic: PngTextEmbedder Service (`/Llm64/Services/PngTextEmbedder.cs`)

Static utility class that handles all PNG manipulation logic. Key methods:

1. **`NormalizePrompt(string prompt)`** - Normalizes user input:
   - Spaces → `+`
   - Newlines → `/`
   - Filters to valid base64 chars only (A-Z, a-z, 0-9, +, /, =)
   - **Critical**: Auto-pads with `+` to ensure length % 4 == 0 (prevents `=` padding corruption)

2. **`EmbedTextInPng(byte[] pngData, string text)`** - Main embedding logic:
   - Validates PNG signature
   - Calculates byte alignment (base64: 3 bytes → 4 chars)
   - Calls `ManuallyDecodeBase64()` to convert text to raw bytes
   - Inserts custom "dATa" ancillary chunk before IEND
   - Returns modified PNG bytes

3. **`ManuallyDecodeBase64(string text)`** - Custom base64 decoder:
   - Converts each base64 char to 6 bits
   - Packs bits into bytes (8 bits per byte)
   - **Why manual?** Standard `Convert.FromBase64String()` with `=` padding corrupts the last byte during round-trip encoding

### PNG Chunk Structure

Uses custom ancillary chunk type **"dATa"** (lowercase 'd' = safe-to-copy):
- Inserted before IEND chunk
- Contains raw bytes that will encode to target text
- Includes proper CRC32 checksum for PNG validity
- Byte alignment padding (null bytes) prepended to align on 3-byte boundary

### Byte Alignment Critical Detail

The position where data bytes start in the PNG affects base64 encoding:
```
Position % 3 == 0: Perfect alignment, no offset needed
Position % 3 == 1: Need 2 padding bytes
Position % 3 == 2: Need 1 padding byte
```

The `CreateDataChunk()` method calculates:
```csharp
int actualDataPosition = insertPosition + 8;  // +4 length, +4 type
int alignment = actualDataPosition % 3;
int paddingNeeded = alignment == 0 ? 0 : (3 - alignment);
```

### UI Layer (`/Llm64/Pages/Index.razor`)

Simple Blazor component:
- File upload via `InputFile` component
- Text input via `textarea`
- Calls `PngTextEmbedder` service methods
- Displays modified image with download link
- Shows base64 excerpt with embedded text highlighted

## Important: Text Padding Behavior

**All normalized text is padded to length % 4 == 0 using `+` characters.**

Examples:
- `"Hello World"` → `"Hello+World+"` (11 → 12 chars)
- `"ABCDE"` → `"ABCDE+++"` (5 → 8 chars)
- `"Test"` → `"Test"` (already 4 chars)

This padding is **required** to avoid base64 `=` padding corruption during decode/re-encode cycles.

## Test Coverage (`/Llm64.Tests/PngTextEmbedderTests.cs`)

34 tests covering:
- PNG validation
- Text normalization and auto-padding
- Byte alignment verification across different positions
- Base64 embedding correctness
- PNG structure preservation
- Edge cases (empty arrays, invalid PNGs, various text lengths)

Critical test: `EmbedTextInPng_TextAppearsInBase64_AtCorrectAlignment` verifies that normalized text appears verbatim in base64 output.

## Troubleshooting

If embedded text appears corrupted in base64 output (e.g., "Hello+worlf" instead of "Hello+world+"):
1. Verify text was padded to length % 4 == 0
2. Check that `ManuallyDecodeBase64()` is being used (not `Convert.FromBase64String()`)
3. Confirm byte alignment padding is calculated correctly
4. Add debug test to verify round-trip: text → decode → encode → verify
