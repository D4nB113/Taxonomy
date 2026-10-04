namespace TaxonomyBuilder;

public sealed class Definition
{
    public string Text { get; set; } = string.Empty;
    public string? Source { get; set; }

    public IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(Text)
            ? ["Definition text is required."]
            : [];
}