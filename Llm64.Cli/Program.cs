using Llm64.Services;

if (args.Length == 0)
{
    ShowUsage();
    return 1;
}

string? inputPath = null;
string? outputPath = null;
string? message = null;

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
    byte[] inputPng = File.ReadAllBytes(inputPath);

    if (!PngTextEmbedder.IsPng(inputPng))
    {
        Console.WriteLine($"Error: Input file is not a valid PNG: {inputPath}");
        return 1;
    }

    // Normalize and show the lines that will be embedded
    var normalizedLines = PngTextEmbedder.NormalizePrompt(message);
    Console.WriteLine($"Normalized message ({normalizedLines.Count} line(s)):");
    foreach (var line in normalizedLines)
    {
        Console.WriteLine($"  {line}");
    }

    byte[] outputPng = PngTextEmbedder.EmbedTextInPng(inputPng, message);

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
    Console.WriteLine("Usage: Llm64.Cli -i <input.png> -o <output.png> -m <message>");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -i <file>    Input PNG file");
    Console.WriteLine("  -o <file>    Output PNG file");
    Console.WriteLine("  -m <text>    Message to embed");
    Console.WriteLine("  -h, --help   Show this help message");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  Llm64.Cli -i input.png -o output.png -m \"my message\"");
}
