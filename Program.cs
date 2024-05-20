using CommandLine;
using DotNet.Globbing;

var parser = Parser.Default.ParseArguments<CommandLineArgs>(args)
    .WithNotParsed(errors =>
    {
        // Handle parsing errors here.
        Console.WriteLine("Error parsing command line arguments");
    });

await parser.WithParsedAsync(ProcessFilesAsync);

async Task ProcessFilesAsync(CommandLineArgs cliArgs)
{
    if (!Directory.Exists(cliArgs.InputPath))
    {
        Console.WriteLine("The specified input directory does not exist.");
        return;
    }

    if (File.Exists(cliArgs.OutputPath) && !string.IsNullOrWhiteSpace(cliArgs.Test))
    {
        if (!cliArgs.OverwriteOutput)
        {
            Console.WriteLine("The specified output file already exists, and the overwrite flag is not set.");
            return;
        }

        Console.WriteLine("The specified output file already exists, overwriting it.");
        File.Delete(cliArgs.OutputPath);
    }

    var concatIgnorePaths = File.Exists(cliArgs.ConcatIgnorePath) ? File.ReadAllText(cliArgs.ConcatIgnorePath) : string.Empty;
    var excludeGlobber = new Globber($"{cliArgs.SkippedPaths} {concatIgnorePaths}");
    var includeGlobber = new Globber(cliArgs.ExplicitInclude ?? string.Empty);
    bool ShouldBeIncluded(string input) => includeGlobber.IsMatch(input) || !excludeGlobber.IsMatch(input);

    if (!string.IsNullOrWhiteSpace(cliArgs.Test))
    {
        Console.WriteLine(ShouldBeIncluded(cliArgs.Test));
        return;
    }

    var directories = Directory.GetDirectories(cliArgs.InputPath, "*", SearchOption.AllDirectories)
        .Concat(new[] { cliArgs.InputPath })
        .Select(d => $"{d}/")
        .Where(ShouldBeIncluded)
        .Order()
        .ToArray();


    await using var outputFile = new StreamWriter(cliArgs.OutputPath, append: true);
    var output = async (string line) =>
    {
        if (cliArgs.WriteToStdOut)
            Console.WriteLine(line);
        else
            await outputFile.WriteLineAsync(line);
    };

    foreach (var directory in directories)
    {
        var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            if (!cliArgs.IncludeBinaryFiles && IsBinary(file))
                continue;

            if (!ShouldBeIncluded(file))
                continue;

            await ProcessAndWriteFileAsync(file, output);
        }
    }

    Console.WriteLine("Processing complete. Output saved to " + cliArgs.OutputPath);
}

async Task ProcessAndWriteFileAsync(string file, Func<string, Task> writeOutput)
{
    try
    {
        var content = await File.ReadAllTextAsync(file);
        await writeOutput(file);
        await writeOutput(content);
        await writeOutput("");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading from file {file}: {ex.Message}");
        await writeOutput($"Error reading file: {ex.Message}");
        await writeOutput("");
    }
}

bool IsBinary(string filePath)
{
    const int sampleSize = 1024; // Number of bytes to sample for checking
    var buffer = new byte[sampleSize];

    try
    {
        int readLength;
        using (var stream = File.OpenRead(filePath))
        {
            readLength = stream.Read(buffer, 0, sampleSize);
        }

        if (readLength == 0)
            return false;
        var nonPrintableCount = buffer.Take(readLength).Count(b => !IsPrintable(b));

        return (double)nonPrintableCount / readLength > 0.3;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error determining file type for {filePath}: {ex.Message}");
        return true;
    }
}

bool IsPrintable(byte b)
{
    // ASCII printable characters range from 0x20 (space) to 0x7E (tilde), including newlines (0x0A and 0x0D)
    return b is >= 0x20 and <= 0x7E or 0x0A or 0x0D;
}

internal class CommandLineArgs
{
    [Option('b', "binary", Required = false, HelpText = "Should this command include binary files?", Default = false)]
    public bool IncludeBinaryFiles { get; set; }

    [Option('s', "skip", Required = false, HelpText = "Which paths should be skipped?", Default = null)]
    public string? SkippedPaths { get; set; }

    [Option('o', "output", Required = false, HelpText = "In which file should output be placed?", Default = "./output.txt")]
    public string OutputPath { get; set; } = "./output.txt";

    [Option('i', "input", Required = false, HelpText = "Which directory should be used as input?", Default = ".")]
    public string InputPath { get; set; } = ".";

    [Option('f', "overwrite", Required = false, HelpText = "Controls if the output file should be overwritten if it exists", Default = true)]
    public bool OverwriteOutput { get; set; } = true;

    [Option('c', "concatIgnore", Required = false, HelpText = "Which path does the .concatIgnore live?", Default = "./.concatIgnore")]

    public string ConcatIgnorePath { get; set; } = "./.concatIgnore";

    [Option('n', "include", Required = false, HelpText = "Explicit include, overrides all exclude matches", Default = null)]

    public string? ExplicitInclude { get; set; }

    [Option('t', "Test", Required = false, HelpText = "Controls if this command should just test a given input path to see if it would be matched.", Default = null)]

    public string? Test { get; set; } = null;

    [Option('w', "WriteStdOut", Required = false, HelpText = "Controls if this command should output to stdout instead.", Default = false)]

    public bool WriteToStdOut { get; set; } = false;
}

internal class Globber
{
    private readonly List<Glob> _globs = new();

    public Globber(string patterns) : this(patterns.Split(Environment.NewLine).SelectMany(s => s.Split(" ")).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
    {
    }

    public Globber(IEnumerable<string> patterns) => _globs.AddRange(patterns.Select(Glob.Parse));

    public bool IsMatch(string path) => _globs.Any(g => g.IsMatch(path));
}