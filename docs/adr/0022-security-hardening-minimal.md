# ADR 0022: Security/Hardening – Minimal (Demo-Grade)

## Status

Accepted (Demo Scope)

## Context and Problem Statement

ServiceDeskLite is a showcase application that runs locally or in a
controlled demo environment. It is not a production system and has no
real users, no sensitive data, and no external attack surface beyond
what a developer deliberately exposes.

However, shipping it with *zero* security artefacts and hardcoded
credentials in source control would be misleading: a reader of the
codebase would not know whether the absence of security is intentional
or an oversight.

Two concrete gaps existed before this ADR:

1. **Config/credential leakage** – the production `appsettings.json`
   contained a plain-text database password committed to source control.
   CORS allowed-origins were hardcoded in `Program.cs` rather than read
   from configuration.

2. **No authentication** – all API endpoints were publicly accessible
   without any credential check.

The goal is to close both gaps at an honest, minimal level – and to
document exactly what is *not* covered and why.

## Decision Drivers

- Keep the implementation surface small; a showcase that bundles a full
  Identity stack obscures the architectural patterns it is meant to
  demonstrate.
- "No half security": every measure that is applied must be complete
  within its stated scope. A partially wired auth pipeline that can be
  bypassed is worse than a clearly documented absent one.
- Secrets must not appear in source-controlled files.
- The chosen approach must be explainable in a single paragraph to a
  developer reading the code for the first time.

## Decision Outcome

### 1. Configuration separation

Sensitive values are removed from committed configuration files.

| File | Purpose |
|---|---|
| `appsettings.json` | Non-sensitive production defaults only (log levels, `AllowedHosts`, `Persistence:Provider`). Connection string uses a placeholder. |
| `appsettings.Development.json` | Development overrides: InMemory provider, localhost CORS origin, localhost API base URL. |
| Environment variables / user secrets | Real credentials (`ConnectionStrings__ServiceDeskLite`, `Auth__ApiKey`) are injected at runtime. |

CORS allowed-origins are read from `Cors:AllowedOrigins` in
configuration so that the development origin (`https://localhost:7023`)
never appears in production-targeted files or in `Program.cs`.

### 2. API Key authentication (demo-grade)

A single static API key is required on every request to the API. The
key is sent by the client in the `X-Api-Key` HTTP header.

**Why API Key and not JWT / ASP.NET Core Identity:**

| Approach | Why not chosen |
|---|---|
| ASP.NET Core Identity | Requires user store, password hashing, login flows — all unrelated to the architectural patterns this project demonstrates. |
| JWT Bearer | Requires token issuance, signing keys, expiry handling. Adds ~300 lines of infrastructure for zero additional security in a single-tenant demo. |
| OAuth 2.0 / OIDC | External dependency (identity provider). Completely out of scope for a local showcase. |
| API Key | One configuration value, one middleware check, zero session state. Clearly demo-grade; no one will mistake it for a production identity system. |

The API key is:
- Configured via `Auth:ApiKey` (injected at runtime; a placeholder is
  committed as documentation only).
- Validated in a slim `ApiKeyMiddleware` that runs early in the
  pipeline.
- Returned as `401 Unauthorized` (plain, no ProblemDetails body) when
  missing or wrong — keeping the middleware dependency-free.
- Exempt for the OpenAPI/Swagger endpoints in Development.

The Blazor Web frontend reads the key from `Auth:ApiKey` and forwards
it on every outbound HTTP call via a `DelegatingHandler`.

### What is intentionally missing

The following belong to a production authentication system but are
**not part of this implementation**:

- User accounts, roles, or claims-based authorization
- Token expiry, refresh, or revocation
- Secure key rotation mechanism
- HTTPS enforcement beyond `UseHttpsRedirection` (TLS termination at
  reverse proxy level is assumed for production)
- Rate limiting or brute-force protection on the API key check
- CSRF protection (not applicable to a stateless API-Key + JSON API)
- Security headers (HSTS, CSP, X-Frame-Options) — intentionally deferred

### Why this satisfies "no half security"

Every measure applied here is complete within its stated scope:

- Config separation is fully enforced: no secret appears in a
  committed file, and the pattern for injecting real values is explicit.
- API key auth is fully wired end-to-end (middleware → header →
  delegating handler). There is no code path that accidentally bypasses
  it in non-Development environments.
- The boundary of "demo-grade" is stated here, not implied.

## Consequences

### Positive Consequences

- No credentials in source control.
- A developer cloning the repo cannot accidentally run the application
  against a production database without explicitly setting credentials.
- API endpoints require a credential — trivially bypassable for
  development, but the *pattern* is in place for a real key in
  production.
- The security boundary is legible: one ADR, one middleware class, one
  delegating handler.

### Negative Consequences

- A static API key shared across all clients provides no per-client
  auditability or revocation.
- The key is present in the Web server's memory and configuration —
  acceptable for a demo, not for a multi-tenant system.

## Re-evaluation Triggers

Revisit when:

1. The application is exposed to an untrusted network — at that point
   JWT Bearer with a proper identity provider is the minimum acceptable
   standard.
2. Multiple clients or users are introduced — per-client keys or
   claims-based identity become necessary.
3. Audit requirements demand per-user action trails — the current
   anonymous audit events would need to carry a subject identity.

## Related

- `src/ServiceDeskLite.Api/appsettings.json` – non-sensitive defaults
- `src/ServiceDeskLite.Api/appsettings.Development.json` – dev overrides
- `src/ServiceDeskLite.Api/Http/Security/ApiKeyMiddleware.cs` – key validation
- `src/ServiceDeskLite.Web/ApiClient/ApiKeyDelegatingHandler.cs` – key forwarding
