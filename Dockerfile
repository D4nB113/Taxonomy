# Build the Blazor WebAssembly UI and ASP.NET Core authentication host.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish TaxonomyBuilder.Server/TaxonomyBuilder.Server.csproj \
    --configuration Release \
    --output /app/publish

# Run the authenticated host; Railway supplies PORT and terminates public HTTPS.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish/ ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "TaxonomyBuilder.Server.dll"]