# Deployment

## Local development

Run the ASP.NET Core host, which serves the Blazor WebAssembly app and handles
the OIDC callback and session cookie:

```sh
dotnet watch --project TaxonomyBuilder.Server/TaxonomyBuilder.Server.csproj
```

Use the HTTPS URL printed by the .NET CLI. To sign in locally, register
`https://localhost:7261/signin-oidc` as an allowed callback URL at the identity
provider and set the OIDC environment variables described below. Without those
settings, the app runs but `/auth/login` returns `503`.

Publish the complete host with:

```sh
dotnet publish TaxonomyBuilder.Server/TaxonomyBuilder.Server.csproj -c Release
```

## Docker test

Build and run the production image from the repository root:

```sh
docker build -t taxonomy-builder .
docker run --rm -p 8080:8080 taxonomy-builder
```

Open `http://localhost:8080`. The ASP.NET Core host serves the WebAssembly
assets, provides the authentication endpoints, and falls back to `index.html`
for app routes such as `/dashboard` and `/builder`.

## Railway deployment

Railway builds the root `Dockerfile`. It publishes the WebAssembly client and
ASP.NET Core server, then runs the server container. Railway terminates HTTPS
and supplies the container's `PORT`; the server honors that port and forwarded
HTTPS headers. Set the service target port to `8080` if Railway requests one.

1. Deploy this repository as a Railway service with the repository root as its
   source directory.
2. In the service's Networking settings, add `app.ontorious.co.uk` as a custom
   domain. Railway will show the DNS target for that hostname and provision its
   HTTPS certificate after DNS resolves.
3. Add the OIDC environment variables below as Railway variables. Keep the
   client secret out of source control.
4. For stable authentication cookies across restarts, attach a Railway volume
   mounted at `/data` and set `DataProtection__KeysPath=/data/dp-keys`. Use one
   server instance unless all instances share the same data-protection keys.

## DNS records

DNS routes hostnames, not URL paths. Both
`https://app.ontorious.co.uk/dashboard` and
`https://app.ontorious.co.uk/builder` use the same `app` DNS record and Railway
service; the ASP.NET Core/Blazor app handles those paths. Do not create separate
DNS records for `/dashboard` or `/builder`.

In the DNS provider for `ontorious.co.uk`, create the following records using
the exact targets shown by each hosting provider:

| Name | Type | Target |
| --- | --- | --- |
| `app` | `CNAME` | The custom-domain target Railway shows for the app service, often a `*.up.railway.app` hostname. |
| `login` | `CNAME` | The custom-domain target shown by the OIDC provider for `login.ontorious.co.uk`. |

The `login` target is provider-specific; copy it from the identity provider's
custom-domain setup rather than guessing or pointing it at Railway. Some
providers also require a TXT ownership-verification record; add every record
they specify. If you later host a separate login application yourself, point
`login` to that app's hosting target instead. Leave existing MX and other mail
records unchanged. If using a DNS proxy such as Cloudflare, use DNS-only mode
until both Railway and the identity provider confirm proxying is supported.

After adding DNS, return to Railway and the identity provider to verify each
domain and wait for their HTTPS certificates to become active.

## Identity provider and Railway settings

Create a confidential OIDC web application with Authorization Code flow and
PKCE. Set its custom login/issuer domain to `login.ontorious.co.uk` and allow
these production URLs:

- Callback URL: `https://app.ontorious.co.uk/signin-oidc`
- App origin: `https://app.ontorious.co.uk`
- Post-logout return URL: `https://login.ontorious.co.uk/`

In the Railway app service, set:

| Variable | Value |
| --- | --- |
| `Authentication__Authority` | `https://login.ontorious.co.uk` (or the exact issuer URL shown by the provider) |
| `Authentication__ClientId` | The OIDC application's client ID |
| `Authentication__ClientSecret` | The OIDC application's client secret, stored as a Railway secret |
| `Authentication__LoginUrl` | `https://login.ontorious.co.uk/` |
| `DataProtection__KeysPath` | `/data/dp-keys` when the Railway volume is mounted at `/data` |

The sign-in screen itself is served by the managed identity provider on
`login.ontorious.co.uk`. The app backend receives the callback on the `app`
subdomain and sets a host-only, secure, HttpOnly session cookie there. Do not
set a parent-domain cookie for `.ontorious.co.uk`.

The taxonomy repository is still in browser memory. This implementation gates
the builder UI but does not yet provide persistent or server-authorized
taxonomy storage; do not treat client-side taxonomy data as private until it
is moved behind authenticated API endpoints.