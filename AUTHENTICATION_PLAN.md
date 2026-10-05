# Authentication and Dashboard Plan

## Goal

Require sign-in before a user can reach the taxonomy builder. After sign-in,
show a dashboard placeholder first, with a clear path to the builder. The public
application will be available at `https://app.ontorious.co.uk`, and the sign-in
experience at `https://login.ontorious.co.uk`.

## Implementation status

- The ASP.NET Core host, OIDC callback, secure app session cookie, authenticated
  dashboard, builder route guard, and Railway container are implemented.
- Sign-in is not active until an OIDC provider is configured and its custom
  domain is attached to `login.ontorious.co.uk`.
- Taxonomy data remains in browser memory. Authentication gates the UI, but
  taxonomy content is not yet stored or authorized by the backend.

## Current constraints

- The web app is a Blazor WebAssembly single-page application.
- Railway runs an ASP.NET Core host for the app and authentication endpoints.
- There is no taxonomy database or protected taxonomy API yet.
- The taxonomy repository is in-memory and runs in the browser. A client-side
  route guard alone is not security: users can still download and inspect the
  app, and any protected data or operations need server-side authorization.

## Recommended architecture

Keep Blazor WebAssembly for the UI with the ASP.NET Core backend now serving the
published UI and handling authentication requests. Use a
managed OpenID Connect identity provider for credentials, with the backend
acting as a Backend-for-Frontend (BFF): it handles the OIDC sign-in callback,
keeps tokens server-side, and gives the browser a secure, `HttpOnly` session
cookie. This avoids storing access tokens in browser storage or implementing
password handling in the app.

Configure `app.ontorious.co.uk` as the Railway app origin and
`login.ontorious.co.uk` as the sign-in origin (or as the managed identity
provider's custom domain). The app backend owns the OIDC callback and its
host-only session cookie; after authentication, the identity provider redirects
back to the exact callback URL on `app.ontorious.co.uk`. Do not share an app
session cookie across the parent domain just to connect the two subdomains.
The login origin may maintain its own identity-provider session for SSO.

## User flow and routes

1. An unauthenticated visit to `https://app.ontorious.co.uk/`,
  `/dashboard`, or `/builder` redirects to the sign-in experience at
  `https://login.ontorious.co.uk` with a validated return destination.
2. The login origin starts or continues OIDC sign-in. The OIDC response returns
  to the backend callback on `app.ontorious.co.uk`, which creates the app's
  secure session and redirects to `/dashboard` by default or to the requested
  protected path.
3. `/dashboard` is the first authenticated page. Initially it is a placeholder
   with the product name, signed-in user's identity, and a primary action to
   open the taxonomy builder.
4. Move the existing builder from `/` to `/builder`. Require an authenticated
   session both in the UI and on any backend API it uses.
5. Provide sign-out, invalidate the app session, optionally end the identity
  provider session, and return the browser to `login.ontorious.co.uk`.

## Implementation phases

### 1. [Implemented] Establish the authenticated host

- Add an ASP.NET Core host project (or convert the deployment host) to serve
  the published WebAssembly assets and expose authentication/API endpoints.
- Replace the nginx-only runtime image with the backend runtime image while
  retaining the existing static asset build and Railway port configuration.
- Add OIDC and cookie-session configuration using environment variables or
  Railway secrets. Keep client secrets and signing/session keys out of source
  control.
- Enforce HTTPS, secure cookie settings, safe local return URLs, and
  anti-forgery protection for state-changing cookie-authenticated requests.

### 2. [Implemented] Add the authenticated entry experience

- Configure `login.ontorious.co.uk` as the managed provider's custom domain or
  deploy the sign-in experience there; include loading, provider error, and
  signed-in states.
- Add `/dashboard` as an authenticated placeholder with user identity and a
  link to continue to `/builder`.
- Move the current builder page to `/builder`; make `/` route to `/dashboard`
  for signed-in users and redirect signed-out users to `login.ontorious.co.uk`.
- Add a shared authentication state and route authorization policy, while
  treating backend authorization as the actual security boundary.
- Add sign-out and handle expired sessions by returning to `/login`.

### 3. [Remaining] Protect taxonomy data and operations

- Replace browser-only `TaxonomyRepository` operations with authenticated API
  calls before treating taxonomy data as private or durable.
- Associate stored taxonomies with the authenticated user (or an explicit
  organization/tenant) and authorize ownership on every read and write.
- Add persistent storage in a later, separately scoped step if taxonomy data
  must survive reloads or be shared across sessions. Until then, document that
  browser memory remains temporary and is not protected server-side.

### 4. [User setup] Deploy and verify on Railway

- Attach `app.ontorious.co.uk` to the app service and `login.ontorious.co.uk`
  to the identity-provider custom domain or sign-in service. Configure DNS,
  HTTPS, and the exact production OIDC callback/return URLs; configure separate
  development URLs.
- Add required OIDC settings and secrets as Railway variables, then deploy the
  backend-backed image.
- Verify direct loads and refreshes for `login.ontorious.co.uk`,
  `app.ontorious.co.uk/dashboard`, and `app.ontorious.co.uk/builder`.
- Verify signed-out redirects, successful sign-in and return routing,
  sign-out, expired-session behavior, and rejection of unauthorized API calls.
- Confirm one user's session cannot access another user's taxonomy data before
  enabling private or persistent taxonomy storage.

## Acceptance criteria

- A signed-out user cannot open the dashboard or builder and is sent to login.
- A signed-in user lands on the dashboard placeholder and can open the builder.
- Sign-out clears the session; refreshing a protected route then requires
  sign-in again.
- The backend validates the OIDC session; the current builder is UI-gated.
  Taxonomy API authorization remains future work because taxonomy operations
  still run in browser memory.
- The app and login work on their specified HTTPS subdomains, including direct
  route loads and refreshes, with secrets configured outside the repository.

## Decisions before implementation

- Choose the managed OIDC identity provider and confirm whether it supports
  `login.ontorious.co.uk` as a custom domain, or whether a separate sign-in app
  must be deployed there.
- Decide whether the first release needs only sign-in gating, or also private,
  persistent taxonomy data. Private data requires the protected API and
  user/tenant ownership checks in phase 3.