using CommandLine;

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

    [Option('c', "concatIgnore", Required = false, HelpText = "Which path does the .concatIgnore live?", Default = "~/ .concatIgnore")]
    public string ConcatIgnorePath { get; set; } = "/Users/DKapellu/bin/.concatIgnore";

    [Option('n', "include", Required = false, HelpText = "Explicit include, overrides all exclude matches", Default = null)]
    public string? ExplicitInclude { get; set; }

    [Option('t', "Test", Required = false, HelpText = "Controls if this command should just test a given input path to see if it would be matched.", Default = null)]
    public string? Test { get; set; } = null;

    [Option('w', "WriteStdOut", Required = false, HelpText = "Controls if this command should output to stdout instead.", Default = false)]
    public bool WriteToStdOut { get; set; } = false;

    [Option('z', "maxSize", Required = false, HelpText = "The maximum size (in bytes) of files to include in the output.", Default = long.MaxValue)]
    public long MaxFileSize { get; set; } = long.MaxValue;

    [Option('k', "chunks", Required = false, HelpText = "The number of evenly sized chunks to split the output into.", Default = 1)]
    public int OutputChunks { get; set; } = 1;
}