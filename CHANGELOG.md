# Changelog

## Unreleased

- **Contract sync**: refreshed `contract/integrations-v1.yaml` from the live server, picking up the `x-integration-scopes` scope catalog annotation added by the platform's 2026-08-20 release (documentation metadata; no API surface or SDK code change).
- **Contract drift check**: a scheduled workflow now diffs the embedded contract against the live server document each weekday, opening a `contract-drift` tracking issue on any difference and closing it once resolved. `scripts/sync-contract.sh` refreshes the embedded copy; see `docs/contract-sync.md` for the full sync and versioning model.

## 1.0.0-beta.2 — 2026-08-17

- **BaseUrl now requires https** on both `TetsIntegrationsClient` and `SsoUrlBuilder`; `http` is accepted only for loopback hosts (localhost, 127.0.0.1, ::1) during local development. Plain-http configurations sent the API key in cleartext, so they now throw `ArgumentException` at construction. Breaking only for that misconfigured http usage.
- **Response buffering caps**: the SDK-owned `HttpClient` now limits response buffering to 32 MiB (injected clients are left to the caller to configure), and `TetsApiException.RawBody` retains at most 64 KiB of an error body, ending with `...[truncated by SDK]` when cut.
- **Release pipeline hardening**: the NuGet Trusted Publishing login action in the release workflow is now pinned to a full commit SHA instead of a floating tag.

## 1.0.0-beta.1 — 2026-08-17

Initial release. Official .NET client for the TeTS Integrations API v1, targeting `netstandard2.0` (.NET Framework 4.6.2+), `net6.0` (.NET 6/7), and `net8.0`.

- **Targets**: added a first-class `net6.0` target — .NET 6/7 consumers no longer pull the `Microsoft.Bcl.AsyncInterfaces` shim into their dependency closure; the full test suite runs on both the .NET 6 and .NET 8 runtimes in CI.

- **Client**: `TetsIntegrationsClient` (`IDisposable`) over `HttpClient`, usable standalone or via DI/`IHttpClientFactory`. Automatic retries with exponential backoff and jitter on `429`/`5xx`/transport failures and `409 IdempotencyRequestInFlight`, honoring `Retry-After` (clamped to 60s). Errors surface as `TetsApiException` with a stable `TetsErrorCode`, `RequestId`, and raw body for support requests. `LastRateLimit` exposes the most recent rate-limit snapshot.
- **Users**: `CreateAsync` (idempotency-key-safe), `GetByExternalIdAsync`, `UpdateAsync` (partial update), `CheckExistsAsync`, `ActivateAsync`/`DeactivateAsync`, and `ListAsync` — the organization staff roster, auto-paginated via cursor; `UserListItem.ExternalId` is null for users not yet linked to the integration (e.g. migrated accounts).
- **Reports**: `GetCompletionsAsync` auto-paginates the completions report via cursor; `GetCompletionsPageAsync` for manual page control.
- **Catalog**: `ListAsync` streams the organization's training catalog, auto-paginated via cursor. Rows carry both platform ids and legacy numeric ids (`LegacyCourseId` = the completions report's `courseId` and SSO's `courseId`/`cid`; `LegacyProgramId` for SSO `programId` deep links), `RenewOnly` flags superseded editions kept for interpreting historical completions, and `ProgramCourses` lists a program's child courses (null for non-programs).
- **SSO**: `SsoUrlBuilder.BuildLaunchUrl` builds signed launch URLs using the legacy Topyx-compatible MD5 scheme, with full parity for JIT profile fields, course/program targets, multi-organization tenants, and iframe embed (with origin-shape validation). `ComputeSignature` is exposed standalone as a cross-check for existing signers. ASCII-only input validation throughout; actionable error message on FIPS-restricted hosts where `MD5.Create()` is unavailable.
- **Samples**: `samples/TeTS.SmokeTest` — a 9-step onboarding/UAT console checklist, env-var driven.
- **CI**: build+test on push/PR; a contract-parity test locks the SDK's operations and models to `contract/integrations-v1.yaml`, failing the build on drift from the server's OpenAPI contract.
