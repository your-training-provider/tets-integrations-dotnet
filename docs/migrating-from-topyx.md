# Migrating from Topyx

For partners moving an existing Topyx-era integration to the TeTS platform.

The TeTS Integrations API v1 replaces the old SOAP/manual Topyx integration surface with a REST API and a signed SSO launch URL that keeps the same signature scheme. You do not need to re-implement your signer; you do need to change the endpoint you call and adopt the new REST calls for provisioning and reporting.

## Flow mapping

| Legacy Topyx flow | TeTS replacement |
|---|---|
| SOAP / manual user provisioning | `client.Users.CreateAsync(...)` (REST) |
| Signed Topyx SSO URL (MD5) | `client.Sso.BuildLaunchUrl(...)` — same MD5 scheme, new endpoint `/api/integrations/v1/sso` |
| Completions report export (SOAP/report) | `client.Reports.GetCompletionsAsync(...)` — cursor-paginated REST |
| Staff roster export (SOAP/report) | `client.Users.ListAsync(...)` — cursor-paginated REST |
| Username availability checks (manual) | `client.Users.CheckExistsAsync(...)` |
| Deactivation (manual request) | `client.Users.DeactivateAsync(...)` |

## What changes in your SSO links

- **Endpoint path.** Point your signed launch URLs at `/api/integrations/v1/sso` instead of the legacy Topyx SSO path.
- **Slug parameter.** Emit `integration=<slug>` instead of `partner=<slug>`. The server still accepts the legacy `partner=` alias, so nothing breaks if you haven't updated it yet — but new integrations should emit `integration=`.
- **Signature semantics are unchanged.** `username`, `timestamp`, `sessionTimeOut`, and `signature` mean exactly what they meant under Topyx, and the signature is computed the same way: `MD5(secret + username + sessionTimeOut + timestamp)`, lowercase hex.
- **`identification` now upserts `externalId`.** If you pass `identification`, TeTS links (or creates) the user with that value as their stable `externalId` — the same field `Users.CreateAsync` and `Users.GetByExternalIdAsync` use. Use the same value you'd otherwise pass to `CreateAsync`'s `ExternalId`.
- **Multi-organization partners** should add `organizationTenantId` to the launch URL (or set `TetsOptions.OrganizationTenantId`) — required once your connection serves more than one organization.
- **URLs expire.** A signed launch URL is only valid for about 5 minutes from its `timestamp`. Build it at click time; don't cache or email it.

## Your identifiers carry over

The identifier you already track per staff member is the identifier TeTS uses. `externalId` is your stable staff ID — the same value your Topyx-era integration stored per user (many partners kept it in a Topyx custom field). You choose it, you keep it, TeTS never rewrites it. It appears as `ExternalId` on REST calls and `identification` on SSO launch URLs.

TeTS user and group ids are UUIDs, but you don't migrate anything to them and never need to persist them — every user-facing SDK call identifies users by *your* `externalId`. For accounts migrated from the legacy platform, the link between your ID and the TeTS account is established automatically the first time that user launches via SSO with `identification` set, or TeTS can bulk-link a whole organization from a CSV of `externalId, username-or-email` pairs before cutover — ask your onboarding contact. [Syncing your staff roster](#syncing-your-staff-roster) shows you which accounts are linked at any time.

## From legacy group IDs to organization tenants

If your per-customer configuration keys on legacy group IDs, the replacement is one config entry per customer, not a data migration:

| Legacy per-customer config | TeTS replacement |
|---|---|
| Group ID on every call | Nothing — the customer's API key is scoped to their organization and resolves it on every REST call |
| — | API key, issued per customer connection |
| — | `organizationTenantId`: one static UUID per customer, needed only on SSO launch URLs when your integration slug serves multiple organizations (and available for REST scoping if you ever share one key across organizations) |

TeTS provides a mapping table (legacy group ID → customer → tenant ID) at onboarding, so populating the config is a lookup. Each entry is self-verifying: call `client.PingAsync()` with the customer's key and it echoes the resolved `OrganizationTenantId` and connection label.

## Syncing your staff roster

`client.Users.ListAsync()` replaces the legacy roster export: it streams every user in your organization, following pagination automatically, so you can reconcile TeTS against your HR system before and after cutover.

```csharp
await foreach (var user in client.Users.ListAsync())
{
    if (user.ExternalId is null)
    {
        // Migrated account not yet linked to your integration.
    }
}
```

Rows with `externalId: null` are accounts migrated from the legacy platform that aren't linked to your integration yet. Linking happens automatically the first time such a user launches via SSO with `identification` set (see above) — or TeTS can bulk-link your accounts from a CSV before cutover; ask your onboarding contact. Filter to one group with `ListUsersOptions.GroupId`.

## Signature compatibility

Your existing MD5 signer keeps working — the wire format is byte-for-byte the same as Topyx's. If you want to verify your signer against the SDK's implementation (or you're not ready to adopt the SDK for signing yet and just want a reference), use the static helper:

```csharp
using TeTS.Integrations.Sso;

var signature = SsoUrlBuilder.ComputeSignature(
    secret: "test-secret", username: "casey.lee",
    sessionTimeOutSeconds: "28800", timestamp: "1783332000");
// => "63d7f0a4afedbc795496be859a186c9f"
```

All signed inputs must be ASCII — the SDK throws rather than guessing at a lossy encoding for non-ASCII usernames or secrets, since Topyx-era platforms and .NET disagree on how to handle high-bit characters in this scheme.

## Not yet in API v1

These Topyx-era capabilities don't have a v1 REST equivalent yet. Ask TeTS about timelines if your integration depends on one:

- Manager/supervisor provisioning via API (learner provisioning only, for now)
- Catalog export
- Certificate download
- Self-service API key rotation
- Push webhooks — completions and status changes are polling-only via `Reports.GetCompletionsAsync`

## Getting help

- **SDK bugs or usage questions** — open an issue on [GitHub Issues](https://github.com/your-training-provider/tets-integrations-dotnet/issues).
- **Credentials, org connections, or timelines for the capabilities above** — reach out to your TeTS onboarding contact.
