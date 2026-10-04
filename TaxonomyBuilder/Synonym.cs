namespace TaxonomyBuilder;

public sealed class Synonym
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Description { get; set; }
    public List<CategoryProperty> Properties { get; set; } = [];

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Synonym name is required.");
        errors.AddRange(CategoryProperty.ValidateCollection(Properties));
        return errors;
    }
}