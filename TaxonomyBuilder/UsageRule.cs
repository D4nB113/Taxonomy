namespace TaxonomyBuilder;

public sealed class UsageRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;

    public IReadOnlyList<string> Validate() =>
        string.IsNullOrWhiteSpace(Text)
            ? ["Usage rule text is required."]
            : [];
}