# TeTS.Integrations

Official .NET client for the TeTS Integrations API v1: user provisioning, completion reports, and signed SSO launch URLs for integration partners.

<!-- badges: add after first publish -->

```
dotnet add package TeTS.Integrations
```

Targets .NET Framework 4.6.2+ (via `netstandard2.0`) and .NET 8+.

Migrating from a legacy Topyx integration? Start with [docs/migrating-from-topyx.md](https://github.com/your-training-provider/tets-integrations-dotnet/blob/main/docs/migrating-from-topyx.md).

## Quickstart

`BaseUrl` and `ApiKey` are issued by TeTS during partner onboarding — see [Getting help](#getting-help) if you don't have them yet.

```csharp
using TeTS.Integrations;
using TeTS.Integrations.Models;
using TeTS.Integrations.Sso;

using var client = new TetsIntegrationsClient(new TetsOptions
{
    BaseUrl = "https://courses.example.com",
    ApiKey = Environment.GetEnvironmentVariable("TETS_API_KEY")!,
});

// 1. Verify your key and resolved organization
var ping = await client.PingAsync();
Console.WriteLine($"Connected to {ping.ConnectionLabel} (tenant {ping.OrganizationTenantId})");

// 2. Check a username before creating it
var exists = await client.Users.CheckExistsAsync("casey.lee");

// 3. Provision a learner (retry-safe: the SDK sends an Idempotency-Key for you)
var user = await client.Users.CreateAsync(new CreateUserRequest
{
    ExternalId = "your-stable-staff-id",
    UserName = "casey.lee",
    FirstName = "Casey",
    LastName = "Lee",
    Email = "casey.lee@example.com",
});

// 4. Poll completions (pagination handled for you)
await foreach (var completion in client.Reports.GetCompletionsAsync(
    DateTime.UtcNow.AddDays(-7), DateTime.UtcNow))
    Console.WriteLine($"{completion.UserName}: {completion.CourseName} @ {completion.CompletedDate}");

// 5. Deactivate a learner who left
await client.Users.DeactivateAsync("your-stable-staff-id");
```

`TetsIntegrationsClient` is `IDisposable` — the `using` above disposes the `HttpClient` it created for you. `Users.CreateAsync` sends an `Idempotency-Key` header automatically, so the SDK's own internal retries on transient failures reuse it and never create a duplicate user. A key is scoped to one `CreateAsync` call, though: if *you* call it again yourself (e.g. after a client-side timeout), that's a fresh key, not a replay — the fallback protection there is `externalId`/email uniqueness, which surfaces as `IntegrationExternalIdTaken` or `UserEmailTaken` instead of silently creating a duplicate.

## Smoke test

The fastest way to verify your staging credentials end-to-end is the bundled smoke test — run it before writing any code:

```
dotnet run --project samples/TeTS.SmokeTest
```

Run this against your staging credentials as onboarding step one. It exercises `ping`, `users/exists`, user creation, lookup by `externalId`, an SSO launch URL (if configured), completions polling, and deactivation — printing `TetsApiException.RequestId` on any failure so you can hand it straight to TeTS (see [Getting help](#getting-help)). It creates exactly one disposable test user and deactivates it at the end; the API deliberately has no delete endpoint, so deactivation is the cleanup step.

| Variable | Required | Purpose |
|---|---|---|
| `TETS_BASE_URL` | Yes | Your platform base URL, e.g. `https://courses.example.com`. |
| `TETS_API_KEY` | Yes | API key issued by TeTS. |
| `TETS_SSO_SECRET` | No | Enables the SSO launch URL step. |
| `TETS_INTEGRATION_SLUG` | No | Enables the SSO launch URL step. |
| `TETS_TENANT_ID` | No | Org root group UUID, for multi-organization integrations. Also applied to the SSO launch URL step — required there when your integration serves multiple organizations. |
| `TETS_COURSE_ID` | No | Course to target in the printed SSO launch URL. |
| `TETS_GROUP_ID` | No | Group to place the test user in. Ask your TeTS onboarding contact for your organization's learner group ID; if omitted, the server default applies. |

## Client lifetime

Create one `TetsIntegrationsClient` and reuse it for the lifetime of your application or DI scope. Constructing a new client per request creates a new `HttpClient` each time, which leaks sockets under load (the classic .NET socket-exhaustion trap) — don't do it.

If you're already using `IHttpClientFactory`, inject the `HttpClient` instead of letting the SDK own one:

```csharp
public class MyService
{
    private readonly TetsIntegrationsClient _client;

    public MyService(IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient("tets");
        _client = new TetsIntegrationsClient(httpClient, new TetsOptions
        {
            BaseUrl = "https://courses.example.com",
            ApiKey = Environment.GetEnvironmentVariable("TETS_API_KEY")!,
        });
    }
}
```

With this constructor the SDK never touches the injected `HttpClient`'s `Timeout`, and disposing the `TetsIntegrationsClient` does not dispose the injected `HttpClient` — you own its lifetime, as `IHttpClientFactory` expects.

## SSO launch

SSO launch URLs require `IntegrationSlug` and `SsoSecret` on `TetsOptions`:

```csharp
using var client = new TetsIntegrationsClient(new TetsOptions
{
    BaseUrl = "https://courses.example.com",
    ApiKey = Environment.GetEnvironmentVariable("TETS_API_KEY")!,
    IntegrationSlug = "acme",
    SsoSecret = Environment.GetEnvironmentVariable("TETS_SSO_SECRET")!,
});

var launchUrl = client.Sso.BuildLaunchUrl(new SsoLaunchRequest
{
    UserName = "casey.lee",
    CourseId = "42",
});

// Interpolate with AbsoluteUri, not ToString() — ToString() un-escapes reserved
// characters and can hand the browser a malformed URL.
Console.WriteLine(launchUrl.AbsoluteUri);
```

Redirect the learner's browser to the returned URL.

### Embedding the player in an iframe

Set `Embed = true` and `EmbedOrigin` to your application's exact origin:

```csharp
var embedUrl = client.Sso.BuildLaunchUrl(new SsoLaunchRequest
{
    UserName = "casey.lee",
    CourseId = "42",
    Embed = true,
    EmbedOrigin = "https://app.partner.example",
});
```

TeTS must approve `EmbedOrigin` on your connection server-side before an embed launch is issued — see [Getting help](#getting-help) to request it. The builder also validates the *shape* of `EmbedOrigin` at build time (must be `https://host`, or `http://localhost`/`http://127.0.0.1` for local dev; no userinfo, path, query, or fragment) so a malformed origin fails fast in your code instead of at the platform.

### Cross-checking an existing signer

If you already have a Topyx-style MD5 signer and want to verify it against the SDK, use `ComputeSignature` directly:

```csharp
var signature = SsoUrlBuilder.ComputeSignature(
    secret: "test-secret", username: "casey.lee",
    sessionTimeOutSeconds: "28800", timestamp: "1783332000");
// => "63d7f0a4afedbc795496be859a186c9f"
```

All SSO inputs (username, secret, and the rest of the signed fields) must be ASCII — non-ASCII characters throw `ArgumentException` rather than silently producing a signature the server can't verify.

### URL TTL

The server rejects a signed launch URL once its timestamp is more than about 5 minutes old. Build the URL at click time — don't generate it ahead of time, cache it, or put it in an email.

## FIPS-enforced Windows hosts

The SSO signature uses MD5 for byte-for-byte compatibility with the legacy Topyx-era signing scheme — this is a protocol compatibility requirement, not a general-purpose hash choice, and it has no bearing on the REST API's security.

On Windows hosts running .NET Framework with the FIPS security policy enforced, `MD5.Create()` throws, and SSO URL building will fail with an `InvalidOperationException` that names this section. REST API calls (`Users`, `Reports`, `PingAsync`) are unaffected — only `Sso.BuildLaunchUrl` and `Sso.ComputeSignature` depend on MD5. If your servers enforce FIPS mode, see [Getting help](#getting-help) to discuss options before you rely on SSO launch.

## Multi-organization tenants

If your integration serves more than one organization, requests need to know which one to scope to. Two ways to set it:

```csharp
// Once, from the connection TeTS provisioned for you (PingAsync reports the resolved tenant):
var ping = await client.PingAsync();
using var scopedClient = new TetsIntegrationsClient(new TetsOptions
{
    BaseUrl = "https://courses.example.com",
    ApiKey = Environment.GetEnvironmentVariable("TETS_API_KEY")!,
    OrganizationTenantId = ping.OrganizationTenantId,
});

// ...or per call, when a single client serves multiple orgs:
var exists = await client.Users.CheckExistsAsync("casey.lee",
    organizationTenantId: ping.OrganizationTenantId);
```

Every `Users` and `Reports` method takes an optional `organizationTenantId` parameter that overrides `TetsOptions.OrganizationTenantId` for that one call.

SSO launch URLs need the same scoping: set `SsoLaunchRequest.OrganizationTenantId` when your integration serves multiple organizations.

```csharp
var launchUrl = client.Sso.BuildLaunchUrl(new SsoLaunchRequest
{
    UserName = "casey.lee", OrganizationTenantId = ping.OrganizationTenantId,
});
```

## Errors

Every API error is a `TetsApiException`:

```csharp
try
{
    await client.Users.CreateAsync(request);
}
catch (TetsApiException ex)
{
    Console.Error.WriteLine($"{ex.Code}: {ex.Message}");
    // Always include RequestId when contacting TeTS — see Getting help.
    Console.Error.WriteLine($"requestId: {ex.RequestId}");
}
```

`ex.Code` is one of the stable `TetsErrorCode` values below. `ex.RequestId` correlates with TeTS server logs — include it in every support request (see [Getting help](#getting-help)).

| `TetsErrorCode` | Meaning | Retry guidance |
|---|---|---|
| `ValidationError` | Request body or query failed validation. | Fix the request; not retried. |
| `IntegrationConnectionRequired` | No active connection exists for this integration/organization. | See [Getting help](#getting-help) to activate the connection; not retried. |
| `IntegrationUserIdentifierRequired` | The request needs `externalId` or `userId` to identify a user. | Fix the request; not retried. |
| `IntegrationBadInput` | Integration-specific input was malformed. | Fix the request; not retried. |
| `Unauthorized` | The API key is missing, invalid, or expired. | Fix the API key; not retried. |
| `IntegrationConnectionForbidden` | Your connection is not allowed to perform this operation. | See [Getting help](#getting-help); not retried. |
| `IntegrationConnectionInactive` | Your connection has been deactivated. | See [Getting help](#getting-help) to reactivate; not retried. |
| `IntegrationUserOutOfScope` | The user is outside your integration's scope (wrong org/connection). | Fix the identifier; not retried. |
| `InsufficientScope` | Your API key lacks the scope this endpoint requires. | Request a key with the right scope; not retried. |
| `IntegrationUserNotFound` | No user found for this integration matching the identifier given. | Verify the identifier, or create the user; not retried. |
| `IdempotencyRequestInFlight` | Another request with the same `Idempotency-Key` is still being processed. | **Retried automatically** by the SDK. |
| `IntegrationExternalIdTaken` | `externalId` is already linked to a different user in this organization. | Resolve the conflict on your side; not retried. |
| `UserEmailTaken` | The email is already in use. | Resolve the conflict on your side; not retried. |
| `UsernameTaken` | The username is already in use. | Resolve the conflict on your side; not retried. |
| `IdempotencyKeyReused` | The same `Idempotency-Key` was sent with a different request body. | Use a new key per distinct request; not retried. |
| `RateLimited` | You've exceeded the rate limit for this key/route. | **Retried automatically** by the SDK, honoring `Retry-After`. |
| `InternalError` | Unexpected server error. | **Retried automatically** by the SDK. Report the `RequestId` (see [Getting help](#getting-help)) if it persists. |
| `FeatureDisabled` | The Integrations API is disabled on this environment. | **Retried automatically** by the SDK (it's a 5xx), but won't succeed until TeTS re-enables it — see [Getting help](#getting-help) if it persists. |
| `Unknown` | A code this SDK version doesn't recognize yet (forward compatibility). | Treat the HTTP status code as authoritative — the SDK still retries it automatically when the status is 429 or 5xx. |
| `PaginationStalled` | Client-side only, never sent by the server: `GetCompletionsAsync` aborted because the server returned the same pagination cursor twice in a row. | Not retried — report the `RequestId`, if any, and the date range to TeTS (see [Getting help](#getting-help)). |

Transport-level failures — no response ever received, e.g. DNS failure, connection refused, or a client-side timeout — surface as `HttpRequestException` or `TaskCanceledException`, not `TetsApiException`, which is reserved for responses the server actually sent.

## Retries & rate limits

By default the client retries a failed request up to 3 times (4 attempts total) with exponential backoff and jitter, for any `429`, any `5xx`, and the specific `409 IdempotencyRequestInFlight` conflict. When the response includes a `Retry-After` header, the SDK honors it instead of its own backoff — clamped to at most 60 seconds, so a misconfigured or hostile response can't stall your process.

Set `MaxRetries = 0` on `TetsOptions` to disable this and own retry logic yourself.

```csharp
using var client = new TetsIntegrationsClient(new TetsOptions
{
    BaseUrl = "https://courses.example.com",
    ApiKey = Environment.GetEnvironmentVariable("TETS_API_KEY")!,
    MaxRetries = 0,
});

await client.PingAsync();
if (client.LastRateLimit is { } rateLimit)
    Console.WriteLine($"{rateLimit.Remaining}/{rateLimit.Limit}, resets at {rateLimit.ResetEpochSeconds}");
```

`client.LastRateLimit` snapshots the rate-limit headers from the most recent response. Under concurrent requests on the same client it's last-writer-wins — treat it as an approximate signal, not a precise per-request value.

## Contract & versioning

The API serves its own OpenAPI contract at `/api/integrations/v1/openapi.yaml`. This SDK is locked to that contract by a CI parity test that fails the build if a model or operation drifts out of sync with the server's document. The package follows semver, and `/v1` routes are stable for the lifetime of v1 — breaking changes ship as a new version prefix, not in place.

One caveat: a few SSO query parameters accepted by the server (profile fields like `firstName`/`lastName`/`email`/`organization`/`jobTitle`, and `courseName`/`contentId`/`programName`) aren't yet listed in the OpenAPI document, even though the endpoint accepts them. `SsoLaunchRequest` supports the full set regardless — the SDK isn't limited to what's currently documented.

## Getting help

- **SDK bugs or usage questions** — open an issue on [GitHub Issues](https://github.com/your-training-provider/tets-integrations-dotnet/issues).
- **Credentials, org connections, FIPS-mode discussions, or feature timelines** — reach out to your TeTS onboarding contact.

## Migrating from Topyx

Moving an existing Topyx-era integration to TeTS? See [docs/migrating-from-topyx.md](https://github.com/your-training-provider/tets-integrations-dotnet/blob/main/docs/migrating-from-topyx.md).
