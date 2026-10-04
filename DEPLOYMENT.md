# Deployment

## Local development

The browser application is the pure Blazor WebAssembly project at
`TaxonomyBuilder.WebApp/TaxonomyBuilder.WebApp.csproj`. It references the
`TaxonomyBuilder` class library and uses the Blazor WebAssembly development
server; the production Docker image does not change that workflow.

From the repository root, start the app with:

```sh
dotnet watch --project TaxonomyBuilder.WebApp/TaxonomyBuilder.WebApp.csproj
```

Open the local URL printed by the .NET CLI. This also works in Visual Studio
Code and GitHub Codespaces with the usual forwarded port. To test the Release
publish locally, run:

```sh
dotnet publish TaxonomyBuilder.WebApp/TaxonomyBuilder.WebApp.csproj -c Release
```

The published static site is written under
`TaxonomyBuilder.WebApp/bin/Release/net10.0/publish/wwwroot`.

## Docker test

Build and run the production image from the repository root:

```sh
docker build -t taxonomy-builder .
docker run --rm -p 8080:80 taxonomy-builder
```

Open `http://localhost:8080`. Refreshing a client-side route such as `/search`
or `/builder` should return `index.html` from nginx, allowing Blazor to handle
the route in the browser. Existing static files are served directly.

## Railway deployment

Railway detects the root-level `Dockerfile` automatically and builds the
application from the repository root. The image publishes the WebAssembly
project with the .NET 10 SDK, then copies only the published `wwwroot` into an
nginx image. The final container serves those static files on port 80; it does
not run an ASP.NET server or require a .NET runtime. During the image build,
the published page's base URL is set to `/` so assets resolve correctly from
deep client-side routes. The source page remains unchanged for local development
and GitHub Pages.

1. Create a Railway project and deploy this repository as a service.
2. Leave the service root directory unset so Railway finds the root-level
   `Dockerfile` and the project-relative build inputs.
3. In the service's Networking settings, generate a public domain. The service
   listens on port 80, which is declared by `EXPOSE 80`; if Railway asks for a
   target port, set it to `80`.

No application environment variables are required. Railway's generated public
URL can be used directly. nginx falls back to the app's `index.html` for
client-side routes so direct navigation and browser refreshes work for routes
such as `/`, `/search`, and `/builder`.