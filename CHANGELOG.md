# Changelog

## 1.0.0-beta.1 — unreleased

Initial release. Official .NET client for the TeTS Integrations API v1, targeting `netstandard2.0` (.NET Framework 4.6.2+), `net6.0` (.NET 6/7), and `net8.0`.

- **Targets**: added a first-class `net6.0` target — .NET 6/7 consumers no longer pull the `Microsoft.Bcl.AsyncInterfaces` shim into their dependency closure; the full test suite runs on both the .NET 6 and .NET 8 runtimes in CI.

- **Client**: `TetsIntegrationsClient` (`IDisposable`) over `HttpClient`, usable standalone or via DI/`IHttpClientFactory`. Automatic retries with exponential backoff and jitter on `429`/`5xx`/transport failures and `409 IdempotencyRequestInFlight`, honoring `Retry-After` (clamped to 60s). Errors surface as `TetsApiException` with a stable `TetsErrorCode`, `RequestId`, and raw body for support requests. `LastRateLimit` exposes the most recent rate-limit snapshot.
- **Users**: `CreateAsync` (idempotency-key-safe), `GetByExternalIdAsync`, `UpdateAsync` (partial update), `CheckExistsAsync`, `ActivateAsync`/`DeactivateAsync`, and `ListAsync` — the organization staff roster, auto-paginated via cursor; `UserListItem.ExternalId` is null for users not yet linked to the integration (e.g. migrated accounts).
- **Reports**: `GetCompletionsAsync` auto-paginates the completions report via cursor; `GetCompletionsPageAsync` for manual page control.
- **SSO**: `SsoUrlBuilder.BuildLaunchUrl` builds signed launch URLs using the legacy Topyx-compatible MD5 scheme, with full parity for JIT profile fields, course/program targets, multi-organization tenants, and iframe embed (with origin-shape validation). `ComputeSignature` is exposed standalone as a cross-check for existing signers. ASCII-only input validation throughout; actionable error message on FIPS-restricted hosts where `MD5.Create()` is unavailable.
- **Samples**: `samples/TeTS.SmokeTest` — an 8-step onboarding/UAT console checklist, env-var driven.
- **CI**: build+test on push/PR; a contract-parity test locks the SDK's operations and models to `contract/integrations-v1.yaml`, failing the build on drift from the server's OpenAPI contract.
