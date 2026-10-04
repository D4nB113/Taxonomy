namespace TaxonomyBuilder;

public sealed class VersionInfo
{
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
    public int VersionNumber { get; set; } = 1;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(CreatedBy)) errors.Add("Created by is required.");
        if (CreatedDate == default) errors.Add("Created date is required.");
        if (VersionNumber < 1) errors.Add("Version number must be at least 1.");
        return errors;
    }
}