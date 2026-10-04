namespace TaxonomyBuilder;

public sealed class Term
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Definition? Definition { get; set; }
    public List<Synonym> Synonyms { get; set; } = [];
    public List<UsageRule> UsageRules { get; set; } = [];
    public VersionInfo VersionInfo { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Term name is required.");
        if (Definition is null || string.IsNullOrWhiteSpace(Definition.Text))
            errors.Add("Term definition is required.");
        else errors.AddRange(Definition.Validate());
        if (VersionInfo is null) errors.Add("Term version information is required.");
        else errors.AddRange(VersionInfo.Validate());
        if (Synonyms is not null)
            errors.AddRange(Synonyms.SelectMany(synonym => synonym.Validate()));
        if (UsageRules is not null)
            errors.AddRange(UsageRules.SelectMany(rule => rule.Validate()));
        return errors;
    }
}