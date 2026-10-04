namespace TaxonomyBuilder;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CategoryProperty> Properties { get; set; } = [];
    public List<Synonym> Synonyms { get; set; } = [];
    public List<Category> Subcategories { get; set; } = [];
    public List<Term> Terms { get; set; } = [];
    public VersionInfo VersionInfo { get; set; } = new();

    public IReadOnlyList<string> Validate() => Validate(new HashSet<Guid>());

    private IReadOnlyList<string> Validate(HashSet<Guid> path)
    {
        if (!path.Add(Id)) return ["Category hierarchy contains a cycle."];

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Category name is required.");
        if (VersionInfo is null) errors.Add("Category version information is required.");
        else errors.AddRange(VersionInfo.Validate());
        errors.AddRange(CategoryProperty.ValidateCollection(Properties));
        if (Synonyms is not null)
            errors.AddRange(Synonyms.SelectMany(synonym => synonym.Validate()));
        if (Terms is not null)
            errors.AddRange(Terms.SelectMany(term => term.Validate()));

        if (Subcategories is not null)
        {
            foreach (var subcategory in Subcategories)
                errors.AddRange(subcategory.Validate(path));
        }

        path.Remove(Id);
        return errors;
    }
}