# Taxonomy Builder

A minimal C# class library for building and querying taxonomies in memory. It provides the core taxonomy domain model and a small repository; it does not include a UI, database integration, or external package dependencies.

## Requirements

- .NET 10 SDK

## Build

From the repository root, run:

```sh
dotnet build TaxonomyBuilder/TaxonomyBuilder.csproj
```

## Console Sample

The `TaxonomyBuilder.ConsoleApp` project is a small executable sample that creates each domain object, sets its properties, validates a term, and exercises the in-memory repository.

Run it from the repository root with:

```sh
dotnet run --project TaxonomyBuilder.ConsoleApp/TaxonomyBuilder.ConsoleApp.csproj
```

## Web App

`TaxonomyBuilder.WebApp` is the Blazor WebAssembly client. `TaxonomyBuilder.Server` hosts that client and provides the OIDC sign-in callback and secure session cookie. The authenticated routes are `/dashboard` and `/builder`; sign-in is provided by the managed OIDC provider on `login.ontorious.co.uk`.

Run it locally with:

```sh
dotnet run --project TaxonomyBuilder.Server/TaxonomyBuilder.Server.csproj
```

Configure the OIDC settings described in [DEPLOYMENT.md](DEPLOYMENT.md) to enable sign-in. The taxonomy repository still stores classes in browser memory, so they are cleared when the app reloads and are not yet protected by server-side data authorization. Each class starts with an empty synonyms list; there is no separate synonym-entry form.

### GitHub Pages

The GitHub Pages workflow, if enabled, only publishes static client assets and cannot provide the server-backed sign-in flow. Use the Railway server deployment for authenticated access.

## Project Structure

The library is in `TaxonomyBuilder/` and uses the `TaxonomyBuilder` namespace.

- `Taxonomy` contains categories and version information.
- `Category` contains terms, an empty-by-default synonyms list, typed `CategoryProperty` definitions, and recursively nested subcategories.
- `CategoryProperty` is a recursive JSON-style node with a key, optional value, value type (`Text`, `Number`, `Boolean`, `Date`, `Time`, `DateTime`, or `Object`), and nested properties.
- `Term` contains a definition, synonyms, usage rules, and version information.
- `Synonym`, `Definition`, and `UsageRule` represent term details.
- `VersionInfo` tracks creator, creation date, and version number.
- `TaxonomyRepository` stores and queries taxonomy objects in memory.

Each domain model provides a `Validate()` method that returns a list of validation messages. Property keys must be unique among siblings, and each leaf property must have a value. Property values support nested JSON-style structures, and categories can have subcategories at arbitrary depth. `VersionInfo.CreatedDate` defaults to UTC now and `VersionNumber` defaults to `1`; set `CreatedBy` when creating versioned objects.

## Example

```csharp
using TaxonomyBuilder;

var repository = new TaxonomyRepository();

var taxonomy = new Taxonomy
{
	Name = "Product Catalog",
	VersionInfo = new VersionInfo { CreatedBy = "catalog-team" }
};
repository.AddTaxonomy(taxonomy);

var category = new Category
{
	Name = "Materials",
	VersionInfo = new VersionInfo { CreatedBy = "catalog-team" }
};
repository.AddCategory(taxonomy.Id, category);

var term = new Term
{
	Name = "Stainless steel",
	Definition = new Definition { Text = "A corrosion-resistant steel alloy." },
	VersionInfo = new VersionInfo { CreatedBy = "catalog-team" }
};
repository.AddTerm(taxonomy.Id, category.Id, term);
repository.AddSynonym(taxonomy.Id, term.Id, new Synonym { Name = "Inox" });

var foundTerm = repository.GetTermByName(taxonomy.Id, "stainless steel");
var categoryTerms = repository.GetTermsByCategory(taxonomy.Id, category.Id);
var validationMessages = repository.ValidateTerm(term);
```

`AddTerm` assigns the category ID before storing the term. Call `ValidateTerm` to retrieve validation messages. The repository also provides `GetTaxonomy` and `GetTaxonomies` for retrieving stored taxonomies.

Use `TaxonomyRepository.AddSubcategory(taxonomyId, parentCategoryId, category)` to add a class below an existing class, or `MoveCategory(taxonomyId, categoryId, newParentCategoryId)` to reparent it. Pass `null` as the new parent ID to move a nested class back to the root. New categories initialize `Synonyms` as an empty list automatically.