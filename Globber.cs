using DotNet.Globbing;

internal class Globber
{
    private readonly List<Glob> _globs = new();

    public Globber(string patterns)
        : this(patterns.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
    {
    }

    public Globber(IEnumerable<string> patterns)
    {
        Patterns = patterns.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        _globs.AddRange(Patterns.Select(Glob.Parse));
    }

    public string[] Patterns { get; }

    public bool IsMatch(string path) => _globs.Any(g => g.IsMatch(path));
}