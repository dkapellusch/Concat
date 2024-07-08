using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public static class Compress
{
   public static string CompressOutput(string input, CompressionLevel level)
    {
        return level switch
        {
            CompressionLevel.None => input,
            CompressionLevel.Low => LowCompress(input),
            CompressionLevel.Medium => MediumCompress(input),
            CompressionLevel.High => HighCompress(input),
            CompressionLevel.Extreme => ExtremeCompress(input),
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };
    }

    private static string LowCompress(string input)
    {
        return input.Pipe(RemoveComments)
                    .Pipe(CompressWhitespace);
    }

    private static string MediumCompress(string input)
    {
        return input.Pipe(RemoveComments)
                    .Pipe(CompressWhitespace)
                    .Pipe(RemoveRepetitiveInfo)
                    .Pipe(AbbreviateCommonWords)
                    ;
    }

    private static string HighCompress(string input)
    {
        return input.Pipe(RemoveComments)
                    .Pipe(CompressWhitespace)
                    .Pipe(ShortenPath)
                    .Pipe(RemoveRepetitiveInfo)
                    .Pipe(AbbreviateCommonWords);
    }

    private static string ExtremeCompress(string input)
    {
        return input.Pipe(RemoveComments)
                    .Pipe(CompressWhitespace)
                    .Pipe(ShortenPath)
                    .Pipe(RemoveRepetitiveInfo)
                    .Pipe(TruncateLongLines)
                    .Pipe(AbbreviateCommonWords)
                    .Pipe(RemoveNonEssentialInformation)
                    .Pipe(SummarizeRepeatedPatterns);
    }

     private static string RemoveNonEssentialInformation(string input)
    {
        // Remove less important parts of the code
        // This is a more aggressive approach and should be used cautiously
        var lines = input.Split('\n');
        var essentialLines = lines.Where(line => 
            !line.TrimStart().StartsWith("using") &&
            !string.IsNullOrWhiteSpace(line) &&
            !line.TrimStart().StartsWith("//") &&
            !line.TrimStart().StartsWith("Console.WriteLine") &&
            !line.Contains("Exception") &&
            !line.Contains("catch") &&
            !line.Contains("try")
        );
        return string.Join("\n", essentialLines);
    }

    private static string RemoveCommonPrefixes(string line)
    {
        return line.TrimStart('.', '/', '\\');
    }

    private static string ShortenPath(string path)
    {
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return path;
        return $"{parts[0]}/.../{parts[parts.Length - 1]}";
    }

    private static string RemoveComments(string line)
    {
        // Remove single-line comments
        line = Regex.Replace(line, @"//.*$", string.Empty);
        // Remove multi-line comments (simplified, might need refinement)
        line = Regex.Replace(line, @"/\*.*?\*/", string.Empty);
        return line;
    }

    private static string TruncateLongLines(string line)
    {
        const int MaxLineLength = 100;
        return line.Length > MaxLineLength ? line.Substring(0, MaxLineLength) + "..." : line;
    }

    private static string CompressWhitespace(string line)
    {
        return Regex.Replace(line, @"\s+", " ").Trim();
    }

    private static string AbbreviateCommonWords(string line)
    {
        var abbreviations = new Dictionary<string, string>
        {
            {"function", "func"},
            {"string", "str"},
            {"number", "num"},
            {"array", "arr"},
            {"object", "obj"},
            {"parameter", "param"},
            {"return", "ret"},
            {"class", "cls"},
            {"interface", "iface"},
            {"implements", "impl"},
            {"constructor", "ctor"},
            {"private", "priv"},
            {"protected", "prot"},
            {"public", "pub"},
            {"static", "stat"},
            {"property", "prop"},
            {"method", "meth"}
        };

        foreach (var abbr in abbreviations)
        {
            line = Regex.Replace(line, $@"\b{abbr.Key}\b", abbr.Value, RegexOptions.IgnoreCase);
        }

        return line;
    }

    private static string RemoveRepetitiveInfo(string line)
    {
        // Remove version numbers
        line = Regex.Replace(line, @"\d+\.\d+\.\d+(\.\d+)?", "X.X.X");

        // Shorten GUIDs
        line = Regex.Replace(line, @"[a-fA-F0-9]{8}-([a-fA-F0-9]{4}-){3}[a-fA-F0-9]{12}", "GUID");

        // Remove timestamps
        line = Regex.Replace(line, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", "TIMESTAMP");

        // Remove common prefixes/suffixes in variable names
        line = Regex.Replace(line, @"\b(get|set|on|handle)([A-Z])", "$2");
        
        // Replace long string literals with placeholders
        line = Regex.Replace(line, "\"[^\"]{20,}\"", "\"...\"");
        
        // Simplify numeric literals
        line = Regex.Replace(line, @"\b\d{5,}\b", "LARGENUM");

        return line;
    }

    private static string SummarizeRepeatedPatterns(string content)
    {
        var lines = content.Split('\n');
        var summarized = new List<string>();
        int repeatCount = 1;

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i] == lines[i - 1])
            {
                repeatCount++;
            }
            else
            {
                if (repeatCount > 1)
                {
                    summarized.Add($"[Previous line repeated {repeatCount} times]");
                    repeatCount = 1;
                }
                summarized.Add(lines[i - 1]);
            }
        }

        // Handle the last line
        if (repeatCount > 1)
            summarized.Add($"[Previous line repeated {repeatCount} times]");
        else
            summarized.Add(lines[lines.Length - 1]);

        return string.Join("\n", summarized);
    }

    private static void ExtractKeyStructures(string line, List<string> structures)
    {
        // Extract class definitions (simplified)
        if (Regex.IsMatch(line, @"class\s+\w+"))
        {
            structures.Add(line);
        }

        // Extract function definitions (simplified)
        if (Regex.IsMatch(line, @"(public|private|protected)?\s*(static)?\s*\w+\s+\w+\s*\([^)]*\)"))
        {
            structures.Add(line);
        }
    }
}

// Extension method to enable method chaining
public static class StringExtensions
{
    public static string Pipe(this string input, Func<string, string> func)
    {
        return func(input);
    }
}

public enum CompressionLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Extreme = 4
}