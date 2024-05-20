using DotNet.Globbing;

internal class Globber
{
    private readonly List<Glob> _globs = new();

    public Globber(string patterns) : this(patterns.Split(Environment.NewLine).SelectMany(s => s.Split(" ")).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
    {
    }

    public Globber(IEnumerable<string> patterns)
    {
        Patterns = patterns.ToArray();
        _globs.AddRange(Patterns.Select(Glob.Parse));
    }

    public string[] Patterns { get; }

    public bool IsMatch(string path) => _globs.Any(g => g.IsMatch(path));
}