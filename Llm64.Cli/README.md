# Llm64.Cli

CLI tool for embedding text into PNGs.

## Usage

```bash
Llm64.Cli -i <input.png> -o <output.png> -m <message>
```

**Options:**
- `-i <file>` - Input PNG
- `-o <file>` - Output PNG
- `-m <text>` - Message to embed

## Examples

```bash
# Simple message
Llm64.Cli -i input.png -o output.png -m "my message"

# Multi-line (spaces become +, newlines preserved)
Llm64.Cli -i input.png -o output.png -m "CLI Test
Message"
```

## Verify

```bash
# View base64 output (76-char lines)
base64 -i output.png | fold -w 76

# Search for embedded text (spaces become +)
base64 -i output.png | grep "CLI+Test"
```
