# CLAUDE.md

Guidance for Claude Code when working with this repository.

## Overview

Embeds text into PNGs such that the text appears verbatim in base64-encoded output at 76-character line boundaries.

**Projects:**
- `Llm64.Wasm` - Blazor WASM (minimal Blazor, mostly static HTML)
- `Llm64.Cli` - CLI tool
- `Llm64.Tests` - Unit tests

## Build & Run

```bash
dotnet build
dotnet test
dotnet run --project Llm64.Wasm/Llm64.Wasm.csproj
dotnet run --project Llm64.Cli/Llm64.Cli.csproj -- -i input.png -o output.png -m "text"
```

## Core Logic

**`Llm64.Wasm/Services/PngTool.cs`** - All PNG manipulation logic

Key points:
- Inserts custom "sLOP" ancillary chunk after IHDR
- Text aligned to 57-byte boundaries (76 base64 chars = 57 bytes)
- Uses `ManuallyDecodeBase64()` because standard .NET `Convert.FromBase64String()` corrupts bytes during round-trip with `=` padding
- Padding pattern: `0xFB 0xEF 0xBE` → `++++` in base64

**UI:**
- `index.html` / `about.html` - Static HTML pages
- `css/common.css` - All styles
- `Pages/Index.razor` - Interactive component (file upload, text processing)

**CLI:**
- `Llm64.Cli/Program.cs` - Command-line interface
