# 1000 Words

A webapp that takes a PNG and adds a blob of binary data to it. The fun part is that when that blob of data is shown in base64 encoding, text shows up. This doesn't change what the image looks like when it's viewed, the text is entirely ignored by most (hopefully all) browsers and tools.

However, if you were to attach this PNG file to an email, then the way email attachments are done means that the text might be visible in the raw email body. Which is fun, right?!

## What does that mean?

When a PNG is base64-encoded (as in email attachments or data URLs), the embedded text appears verbatim in the base64 output at standard 76-character line boundaries.

Start with a really simple image, a [1x1 pixel white square](Llm64.Wasm/wwwroot/img/1x1.png) (67 bytes):

```
iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAACklEQVR4nGMAAQAABQABDQottAAA
AABJRU5ErkJggg==
```

This [next image](Llm64.Wasm/wwwroot/img/1x1plus.png) is also a 1x1 pixel white square (836 bytes):

```
iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAC9XNMT1AA++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
All+nontrivial+abstractions+to+some+degree+are+leaky++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
Abstractions+fail+Sometimes+a+little+sometimes+a+lot+Theres+++++++++++++++++
leakage+Things+go+wrong+It+happens+all+over+the+place+when++++++++++++++++++
you+have+abstractions+++++++++++++++++++++++++++++++++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
Joel+Spolsky+2002+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
IxEC/AAAAApJREFUeJxjAAEAAAUAAQ0KLbQAAAAASUVORK5CYII=
```

## How does it work?

We take advantage of a few things:
- PNG files allow us to add our own blocks of data without affecting the image itself
- base64 encodes data by converting bytes to ascii characters `A..Z`, `a..z`, `0..9` as well as `+` and `/`
- email programs and browsers put attachments in base64 blobs

The tool adds a custom "sLOP" ancillary chunk to the PNG containing your text aligned to 57-byte boundaries (76 base64 chars = 57 bytes). Each line of text appears at the start of a base64 line in the output.

## Projects

- **Llm64.Wasm** - Blazor WebAssembly application (runs entirely in your browser, no server processing)
- **Llm64.Cli** - Command-line interface
- **Llm64.Tests** - Unit tests

## Build & Run

```bash
# Build
dotnet build

# Run web app
dotnet run --project Llm64.Wasm/Llm64.Wasm.csproj

# Run CLI
dotnet run --project Llm64.Cli/Llm64.Cli.csproj -- -i input.png -o output.png -m "message"

# Run tests
dotnet test
```

## Deployment

GitHub Actions workflow deploys to S3:
- `release` branch → `/1000-words/`
- `staging` branch → `/1000-words-staging/`

See `.github/workflows/deploy-s3.yml` for details.

## License

MIT
