using Llm64.Wasm.Services;

if (args.Length == 0)
{
    ShowUsage();
    return 1;
}

string? inputPath = null;
string? outputPath = null;
string? message = null;
EmbeddingStyle style = EmbeddingStyle.Email; // Default to Email

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-i" when i + 1 < args.Length:
            inputPath = args[++i];
            break;
        case "-o" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "-m" when i + 1 < args.Length:
            message = args[++i];
            break;
        case "-s":
        case "--style" when i + 1 < args.Length:
            string styleArg = args[++i].ToLowerInvariant();
            style = styleArg switch
            {
                "email" => EmbeddingStyle.Email,
                "compact" => EmbeddingStyle.Compact,
                _ => throw new ArgumentException($"Invalid style: {args[i]}. Use 'email' or 'compact'.")
            };
            break;
        case "-h":
        case "--help":
            ShowUsage();
            return 0;
        default:
            Console.WriteLine($"Unknown argument: {args[i]}");
            ShowUsage();
            return 1;
    }
}

if (inputPath == null || outputPath == null || message == null)
{
    Console.WriteLine("Error: Missing required arguments");
    ShowUsage();
    return 1;
}

if (!File.Exists(inputPath))
{
    Console.WriteLine($"Error: Input file not found: {inputPath}");
    return 1;
}

try
{
    var pngHelper = new PngTool(Base64Mode.Standard);
    byte[] inputPng = File.ReadAllBytes(inputPath);

    if (!pngHelper.IsPng(inputPng))
    {
        Console.WriteLine($"Error: Input file is not a valid PNG: {inputPath}");
        return 1;
    }

    // Normalize and show the lines that will be embedded
    var normalizedLines = pngHelper.NormalizePrompt(message);
    Console.WriteLine($"Embedding style: {style}");
    Console.WriteLine($"Normalized message ({normalizedLines.Count} line(s)):");
    foreach (var line in normalizedLines)
    {
        Console.WriteLine($"  {line}");
    }

    byte[] outputPng = pngHelper.EmbedTextInPng(inputPng, message, style);

    File.WriteAllBytes(outputPath, outputPng);

    Console.WriteLine($"Successfully embedded message in PNG");
    Console.WriteLine($"Output written to: {outputPath}");

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

static void ShowUsage()
{
    Console.WriteLine("Llm64.Cli - PNG Base64 Text Embedder");
    Console.WriteLine();
    Console.WriteLine("Usage: Llm64.Cli -i <input.png> -o <output.png> -m <message> [-s <style>]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -i <file>      Input PNG file");
    Console.WriteLine("  -o <file>      Output PNG file");
    Console.WriteLine("  -m <text>      Message to embed");
    Console.WriteLine("  -s, --style    Embedding style: 'email' (default) or 'compact'");
    Console.WriteLine("  -h, --help     Show this help message");
    Console.WriteLine();
    Console.WriteLine("Embedding Styles:");
    Console.WriteLine("  email    76-character blocks with padding (RFC 2045)");
    Console.WriteLine("  compact  Minimal padding for inline data URLs");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  Llm64.Cli -i input.png -o output.png -m \"my message\"");
    Console.WriteLine("  Llm64.Cli -i input.png -o output.png -m \"my message\" -s compact");
}
