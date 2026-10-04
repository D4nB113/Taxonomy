# Build and publish the pure Blazor WebAssembly application with the .NET 10 SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish TaxonomyBuilder.WebApp/TaxonomyBuilder.WebApp.csproj \
    --configuration Release \
    --output /app/publish

# Use the domain root for assets when a client-side route is loaded directly.
RUN sed -i 's|<base href="\./" />|<base href="/" />|' /app/publish/wwwroot/index.html

# Serve only the published static site from nginx; no .NET runtime is needed.
FROM nginx:alpine AS runtime
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot/ /usr/share/nginx/html/

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]