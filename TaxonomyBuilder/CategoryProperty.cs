using System.Globalization;

namespace TaxonomyBuilder;

public enum CategoryPropertyDataType
{
    Text,
    Number,
    Boolean,
        Date,
    Time,
    DateTime,
    Object
}

public sealed class CategoryProperty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public CategoryPropertyDataType ValueType { get; set; } = CategoryPropertyDataType.Text;
    public List<CategoryProperty> Properties { get; set; } = [];

    public IReadOnlyList<string> Validate() => Validate(new HashSet<Guid>());

    private IReadOnlyList<string> Validate(HashSet<Guid> path)
    {
        if (!path.Add(Id)) return ["Category property hierarchy contains a cycle."];

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Key)) errors.Add("Category property key is required.");
        if ((Properties?.Count ?? 0) == 0 && string.IsNullOrWhiteSpace(Value))
            errors.Add($"Category property '{Key}' requires a value or nested properties.");
        if (!Enum.IsDefined(typeof(CategoryPropertyDataType), ValueType))
            errors.Add("Category property data type is invalid.");
        if (!string.IsNullOrWhiteSpace(Value) && !HasValidValue())
            errors.Add($"Category property '{Key}' must contain a valid {ValueType} value.");
        if (Properties is not null)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in Properties)
            {
                errors.AddRange(property.Validate(path));
                if (!string.IsNullOrWhiteSpace(property.Key) && !keys.Add(property.Key.Trim()))
                    errors.Add($"Category property key '{property.Key}' is duplicated.");
            }
        }

        path.Remove(Id);
        return errors;
    }

    private bool HasValidValue() => ValueType switch
    {
        CategoryPropertyDataType.Text or CategoryPropertyDataType.Object => true,
        CategoryPropertyDataType.Number => decimal.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        CategoryPropertyDataType.Boolean => bool.TryParse(Value, out _),
        CategoryPropertyDataType.Date => DateOnly.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        CategoryPropertyDataType.Time => TimeOnly.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        CategoryPropertyDataType.DateTime => DateTime.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
        _ => false
    };

    public static IReadOnlyList<string> ValidateCollection(IEnumerable<CategoryProperty>? properties)
    {
        var errors = new List<string>();
        if (properties is null) return errors;

        var path = new HashSet<Guid>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
        {
            errors.AddRange(property.Validate(path));
            if (!string.IsNullOrWhiteSpace(property.Key) && !keys.Add(property.Key.Trim()))
                errors.Add($"Category property key '{property.Key}' is duplicated.");
        }

        return errors;
    }
}