# Llm64.Cli - PNG Base64 Text Embedder CLI

Command-line interface for embedding text into PNG files such that the text appears in the base64 encoding with proper line layout at 76-character boundaries.

## Usage

```bash
Llm64.Cli -i <input.png> -o <output.png> -m <message>
```

## Options

- `-i <file>` - Input PNG file
- `-o <file>` - Output PNG file
- `-m <text>` - Message to embed
- `-h, --help` - Show help message

## Examples

```bash
# Embed a simple message
Llm64.Cli -i input.png -o output.png -m "my message"

# Embed a multi-line message
Llm64.Cli -i input.png -o output.png -m "CLI Test
Message"

# Embed a longer message with spaces
Llm64.Cli -i photo.png -o photo_embedded.png -m "Hello World Test"
```

## Building

```bash
dotnet build Llm64.Cli.csproj
```

## Running

```bash
# Using dotnet run
dotnet run --project Llm64.Cli.csproj -- -i input.png -o output.png -m "message"

# Using the built executable
./bin/Debug/net10.0/Llm64.Cli -i input.png -o output.png -m "message"
```

## Text Normalization

The CLI automatically normalizes input text:
- Spaces → `+`
- Newlines preserved as separate lines
- Invalid base64 characters are removed
- Each line padded to exactly 76 characters
- Lines longer than 76 chars wrap to multiple lines

Examples:
- `"Hello World"` → `"Hello+World"` (padded to 76 chars)
- `"CLI Test\nMessage"` → Two lines: `"CLI+Test"` and `"Message"` (each padded to 76 chars)

## Verification

Verify the embedded text appears in the base64 encoding at 76-char line boundaries:

```bash
# View base64 with 76-char line width (standard format)
base64 -i output.png | fold -w 76

# Search for embedded text
base64 -i output.png | grep "CLI+Test"
```

Each text line will appear at the start of a 76-character base64 line.
