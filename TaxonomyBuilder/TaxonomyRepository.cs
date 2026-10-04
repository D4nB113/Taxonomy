namespace TaxonomyBuilder;

public sealed class TaxonomyRepository
{
    private readonly Dictionary<Guid, Taxonomy> _taxonomies = [];
    private readonly object _gate = new();

    public void AddTaxonomy(Taxonomy taxonomy)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);
        var errors = taxonomy.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(taxonomy));

        lock (_gate)
        {
            if (!_taxonomies.TryAdd(taxonomy.Id, taxonomy))
                throw new InvalidOperationException($"A taxonomy with ID '{taxonomy.Id}' already exists.");
        }
    }

    public Taxonomy? GetTaxonomy(Guid taxonomyId)
    {
        lock (_gate)
            return _taxonomies.GetValueOrDefault(taxonomyId);
    }

    public IReadOnlyList<Taxonomy> GetTaxonomies()
    {
        lock (_gate)
            return _taxonomies.Values.ToArray();
    }

    public void AddCategory(Guid taxonomyId, Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        var errors = category.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(category));

        lock (_gate)
        {
            GetTaxonomyOrThrow(taxonomyId).Categories.Add(category);
        }
    }

    public void AddSubcategory(Guid taxonomyId, Guid parentCategoryId, Category subcategory)
    {
        ArgumentNullException.ThrowIfNull(subcategory);
        var errors = subcategory.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(subcategory));

        lock (_gate)
        {
            var taxonomy = GetTaxonomyOrThrow(taxonomyId);
            var parent = GetCategoryOrThrow(taxonomyId, parentCategoryId);
            var existingIds = EnumerateCategories(taxonomy.Categories).Select(category => category.Id).ToHashSet();
            if (EnumerateCategories([subcategory]).Any(category => existingIds.Contains(category.Id)))
                throw new InvalidOperationException("A category with the same ID already exists in this taxonomy.");

            parent.Subcategories.Add(subcategory);
        }
    }

    public void MoveCategory(Guid taxonomyId, Guid categoryId, Guid? newParentCategoryId)
    {
        lock (_gate)
        {
            var taxonomy = GetTaxonomyOrThrow(taxonomyId);
            var category = GetCategoryOrThrow(taxonomyId, categoryId);
            var currentParent = GetCategoryParent(taxonomy.Categories, categoryId);

            if (newParentCategoryId is null)
            {
                if (currentParent is null) return;

                currentParent.Subcategories.Remove(category);
                taxonomy.Categories.Add(category);
                return;
            }

            var newParent = GetCategoryOrThrow(taxonomyId, newParentCategoryId.Value);
            if (newParent.Id == category.Id)
                throw new InvalidOperationException("A category cannot be moved into itself.");
            if (EnumerateCategories([category]).Any(descendant => descendant.Id == newParent.Id))
                throw new InvalidOperationException("A category cannot be moved into one of its descendants.");
            if (currentParent?.Id == newParent.Id) return;

            if (currentParent is null)
                taxonomy.Categories.Remove(category);
            else
                currentParent.Subcategories.Remove(category);

            newParent.Subcategories.Add(category);
        }
    }

    public void AddTerm(Guid taxonomyId, Guid categoryId, Term term)
    {
        ArgumentNullException.ThrowIfNull(term);

        lock (_gate)
        {
            var category = GetCategoryOrThrow(taxonomyId, categoryId);
            term.CategoryId = categoryId;
            ValidateTerm(term);
            category.Terms.Add(term);
        }
    }

    public void AddSynonym(Guid taxonomyId, Guid termId, Synonym synonym)
    {
        ArgumentNullException.ThrowIfNull(synonym);
        var errors = synonym.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(synonym));

        lock (_gate)
        {
            var term = GetTaxonomyOrThrow(taxonomyId).Categories
                .SelectMany(category => EnumerateCategories([category]))
                .SelectMany(category => category.Terms)
                .FirstOrDefault(candidate => candidate.Id == termId)
                ?? throw new KeyNotFoundException($"Term '{termId}' was not found.");
            term.Synonyms.Add(synonym);
        }
    }

    public Term? GetTermByName(Guid taxonomyId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
            return GetTaxonomyOrThrow(taxonomyId).Categories
                .SelectMany(category => EnumerateCategories([category]))
                .SelectMany(category => category.Terms)
                .FirstOrDefault(term => string.Equals(term.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<Term> GetTermsByCategory(Guid taxonomyId, Guid categoryId)
    {
        lock (_gate)
            return GetCategoryOrThrow(taxonomyId, categoryId).Terms.ToArray();
    }

    public IReadOnlyList<string> ValidateTerm(Term term)
    {
        ArgumentNullException.ThrowIfNull(term);
        return term.Validate();
    }

    private Taxonomy GetTaxonomyOrThrow(Guid taxonomyId) =>
        _taxonomies.GetValueOrDefault(taxonomyId)
        ?? throw new KeyNotFoundException($"Taxonomy '{taxonomyId}' was not found.");

    private Category GetCategoryOrThrow(Guid taxonomyId, Guid categoryId) =>
        EnumerateCategories(GetTaxonomyOrThrow(taxonomyId).Categories)
            .FirstOrDefault(category => category.Id == categoryId)
        ?? throw new KeyNotFoundException($"Category '{categoryId}' was not found in taxonomy '{taxonomyId}'.");

    private static Category? GetCategoryParent(IEnumerable<Category> categories, Guid childId)
    {
        foreach (var category in categories)
        {
            if (category.Subcategories.Any(subcategory => subcategory.Id == childId))
                return category;

            var parent = GetCategoryParent(category.Subcategories, childId);
            if (parent is not null) return parent;
        }

        return null;
    }

    private static IEnumerable<Category> EnumerateCategories(IEnumerable<Category> categories)
    {
        foreach (var category in categories)
        {
            yield return category;
            foreach (var subcategory in EnumerateCategories(category.Subcategories))
                yield return subcategory;
        }
    }
}