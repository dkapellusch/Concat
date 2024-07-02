using CommandLine;
using static Compress;

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

    if (File.Exists(cliArgs.OutputPath) && string.IsNullOrWhiteSpace(cliArgs.Test))
    {
        if (!cliArgs.OverwriteOutput)
        {
            Console.WriteLine("The specified output file already exists, and the overwrite flag is not set.");
            return;
        }

        Console.WriteLine("The specified output file already exists, overwriting it.");
        File.Delete(cliArgs.OutputPath);
    }

    string concatIgnorePaths;
    if (File.Exists(cliArgs.ConcatIgnorePath))
    {
        concatIgnorePaths = await File.ReadAllTextAsync(cliArgs.ConcatIgnorePath);
    }
    else
    {
        Console.WriteLine($"Warning: .concatIgnore file not found at {cliArgs.ConcatIgnorePath}. Using default exclude patterns.");
        concatIgnorePaths = string.Empty;
    }

    var excludeGlobber = new Globber($"{cliArgs.SkippedPaths} {concatIgnorePaths}");
    var includeGlobber = new Globber(cliArgs.ExplicitInclude ?? string.Empty);
    
    bool ShouldBeIncluded(string input)
    {
        if (cliArgs.IgnoreHidden && IsHidden(input))
            return false;
        
        return includeGlobber.IsMatch(input) || !excludeGlobber.IsMatch(input);
    }

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
        .Where(d => !cliArgs.IgnoreHidden || !IsHidden(d))
        .Where(ShouldBeIncluded)
        .Order()
        .ToArray();

    var outputFileIndex = 0;
    var currentOutputPath = cliArgs.OutputPath;
    var currentOutputSize = 0L;
    var totalSize = directories.Select(directory => Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
        .SelectMany(files => files.Where(file => (cliArgs.IncludeBinaryFiles || !IsBinary(file)) && new FileInfo(file).Length <= cliArgs.MaxFileSize).Where(ShouldBeIncluded))
        .Sum(file => new FileInfo(file).Length);


    var targetChunkSize = cliArgs.OutputChunks > 1 ? (totalSize + cliArgs.OutputChunks - 1) / cliArgs.OutputChunks : long.MaxValue;

    var outputFile = new StreamWriter(currentOutputPath, true);

    var output = async (string line) =>
    {
        var lineSize = System.Text.Encoding.UTF8.GetByteCount(line + Environment.NewLine);

        if (currentOutputSize + lineSize > targetChunkSize && outputFileIndex < cliArgs.OutputChunks)
        {
            outputFile.Dispose();
            outputFileIndex++;
            currentOutputPath = $"{Path.GetDirectoryName(cliArgs.OutputPath)}/{Path.GetFileNameWithoutExtension(cliArgs.OutputPath)}{outputFileIndex}{Path.GetExtension(cliArgs.OutputPath)}";
            outputFile = new StreamWriter(currentOutputPath, true);
            currentOutputSize = 0;
        }

        if (cliArgs.WriteToStdOut)
            Console.WriteLine(line);
        else
        {
            if (cliArgs.CompressOutput)
                line = CompressOutput(line, cliArgs.CompressionLevel);
            await outputFile.WriteLineAsync(line);
        }

        currentOutputSize += lineSize;
    };

     foreach (var directory in directories)
    {
        var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);

        foreach (var file in files.Where(file => 
            (cliArgs.IncludeBinaryFiles || !IsBinary(file)) && 
            new FileInfo(file).Length <= cliArgs.MaxFileSize &&
            (!cliArgs.IgnoreHidden || !IsHidden(file)) &&
            ShouldBeIncluded(file)))
        {
            await ProcessAndWriteFileAsync(file, output, cliArgs.MaxFileSize);
        }
    }

    outputFile?.Dispose();
    Console.WriteLine("Processing complete. Output saved to " + cliArgs.OutputPath);
}

async Task ProcessAndWriteFileAsync(string file, Func<string, Task> writeOutput, long maxSize)
{
    try
    {
        var content = await File.ReadAllTextAsync(file);
        var fileSize = System.Text.Encoding.UTF8.GetByteCount(content);

        if (fileSize > maxSize)
        {
            throw new Exception(
                $"File '{file}''s size {fileSize} exceeds the maximum output file size of {maxSize} bytes. To include this file please increase the maximum size using the --size flag.");
        }

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


bool IsHidden(string path)
{
    return Path.GetFileName(path).StartsWith(".");
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

