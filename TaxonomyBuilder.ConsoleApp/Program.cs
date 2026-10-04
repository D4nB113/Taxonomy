using TaxonomyBuilder;

var repository = new TaxonomyRepository();
var createdDate = DateTimeOffset.UtcNow;

var taxonomy = new Taxonomy
{
    Id = Guid.NewGuid(),
    Name = "Product Catalog",
    Description = "A sample product taxonomy.",
    Categories = [],
    VersionInfo = new VersionInfo
    {
        CreatedBy = "taxonomy-demo",
        CreatedDate = createdDate,
        VersionNumber = 1
    }
};
repository.AddTaxonomy(taxonomy);

var category = new Category
{
    Id = Guid.NewGuid(),
    Name = "Materials",
    Description = "Material types used in products.",
    Properties =
    [
        new CategoryProperty
        {
            Key = "supplier",
            ValueType = CategoryPropertyDataType.Object,
            Properties =
            [
                new CategoryProperty { Key = "sku", Value = "MT-SS-304" },
                new CategoryProperty
                {
                    Key = "lastVerified",
                    Value = createdDate.ToString("O"),
                    ValueType = CategoryPropertyDataType.DateTime
                }
            ]
        },
        new CategoryProperty
        {
            Key = "unitCost",
            Value = "12.50",
            ValueType = CategoryPropertyDataType.Number
        }
    ],
    Terms = [],
    VersionInfo = new VersionInfo
    {
        CreatedBy = "taxonomy-demo",
        CreatedDate = createdDate,
        VersionNumber = 1
    }
};
repository.AddCategory(taxonomy.Id, category);

var preCast = new Category
{
    Name = "Pre-cast",
    Description = "Materials prepared in a mold before installation.",
    VersionInfo = new VersionInfo { CreatedBy = "taxonomy-demo", CreatedDate = createdDate }
};
repository.AddCategory(taxonomy.Id, preCast);
repository.MoveCategory(taxonomy.Id, preCast.Id, category.Id);
repository.AddSubcategory(taxonomy.Id, category.Id, new Category
{
    Name = "High strength",
    Description = "Materials classified for high-strength applications.",
    VersionInfo = new VersionInfo { CreatedBy = "taxonomy-demo", CreatedDate = createdDate }
});

try
{
    repository.MoveCategory(taxonomy.Id, category.Id, preCast.Id);
    Console.WriteLine("Cycle prevention: failed");
    return 1;
}
catch (InvalidOperationException)
{
    Console.WriteLine("Cycle prevention: passed");
}

var synonym = new Synonym
{
    Id = Guid.NewGuid(),
    Name = "Inox",
    Language = "en"
};

var term = new Term
{
    Id = Guid.NewGuid(),
    CategoryId = category.Id,
    Name = "Stainless steel",
    Definition = new Definition
    {
        Text = "A corrosion-resistant steel alloy.",
        Source = "Sample taxonomy"
    },
    Synonyms = [],
    UsageRules =
    [
        new UsageRule
        {
            Id = Guid.NewGuid(),
            Text = "Use for stainless steel materials, not surface finishes."
        }
    ],
    VersionInfo = new VersionInfo
    {
        CreatedBy = "taxonomy-demo",
        CreatedDate = createdDate,
        VersionNumber = 1
    }
};
repository.AddTerm(taxonomy.Id, category.Id, term);
repository.AddSynonym(taxonomy.Id, term.Id, synonym);

var validationMessages = repository.ValidateTerm(term);
if (validationMessages.Count > 0)
{
    Console.WriteLine("Term validation failed:");
    foreach (var message in validationMessages)
        Console.WriteLine($"- {message}");

    return 1;
}

var retrievedTerm = repository.GetTermByName(taxonomy.Id, term.Name);
var categoryTerms = repository.GetTermsByCategory(taxonomy.Id, category.Id);

Console.WriteLine($"Taxonomy: {taxonomy.Name} ({taxonomy.Id})");
Console.WriteLine($"Description: {taxonomy.Description}");
Console.WriteLine($"Created by: {taxonomy.VersionInfo.CreatedBy}");
Console.WriteLine($"Version: {taxonomy.VersionInfo.VersionNumber} at {taxonomy.VersionInfo.CreatedDate:O}");
Console.WriteLine($"Category: {category.Name} ({category.Id})");
Console.WriteLine($"Category description: {category.Description}");
Console.WriteLine($"Category properties: {string.Join(", ", category.Properties.Select(property => $"{property.Key} [{property.ValueType}]"))}");
Console.WriteLine($"Class synonyms initialized empty: {category.Synonyms.Count == 0}");
Console.WriteLine($"Nested classes: {string.Join(", ", category.Subcategories.Select(subcategory => subcategory.Name))}");
Console.WriteLine($"Terms in category: {categoryTerms.Count}");

if (retrievedTerm is not null)
{
    Console.WriteLine($"Term: {retrievedTerm.Name} ({retrievedTerm.Id})");
    Console.WriteLine($"Definition: {retrievedTerm.Definition?.Text}");
    Console.WriteLine($"Definition source: {retrievedTerm.Definition?.Source}");
    Console.WriteLine($"Synonyms: {string.Join(", ", retrievedTerm.Synonyms.Select(item => $"{item.Name} [{item.Language}]"))}");
    Console.WriteLine($"Usage rules: {string.Join("; ", retrievedTerm.UsageRules.Select(rule => rule.Text))}");
    Console.WriteLine($"Term created by: {retrievedTerm.VersionInfo.CreatedBy}");
    Console.WriteLine($"Term version: {retrievedTerm.VersionInfo.VersionNumber} at {retrievedTerm.VersionInfo.CreatedDate:O}");
}

return 0;