namespace TaxonomyBuilder;

public sealed class Taxonomy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Category> Categories { get; set; } = [];
    public VersionInfo VersionInfo { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Taxonomy name is required.");
        if (VersionInfo is null) errors.Add("Taxonomy version information is required.");
        else errors.AddRange(VersionInfo.Validate());
        if (Categories is not null)
            errors.AddRange(Categories.SelectMany(category => category.Validate()));
        return errors;
    }
}