using CommandLine;

var parser = Parser.Default.ParseArguments<CommandLineArgs>(args)
    .WithNotParsed(errors => { Console.WriteLine("Error parsing command line arguments"); });

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
        Console.WriteLine($"Matches: {ShouldBeIncluded(cliArgs.Test)}");
        Console.WriteLine($"Includes: {string.Join(" ", includeGlobber.Patterns)}");
        Console.WriteLine($"Exludes: {string.Join(" ", excludeGlobber.Patterns)}");
        return;
    }

    var directories = Directory.GetDirectories(cliArgs.InputPath, "*", SearchOption.AllDirectories)
        .Concat(new[] { cliArgs.InputPath })
        .Select(d => $"{d}/")
        .Where(ShouldBeIncluded)
        .Order()
        .ToArray();


    await using var outputFile = new StreamWriter(cliArgs.OutputPath, true);
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

        foreach (var file in files.Where(file => cliArgs.IncludeBinaryFiles || !IsBinary(file)).Where(ShouldBeIncluded))
        {
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
    const int sampleSize = 1024;
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