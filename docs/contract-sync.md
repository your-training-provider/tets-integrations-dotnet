# Keeping the SDK in sync with the platform

How this SDK stays current as the TeTS platform ships releases, and what a maintainer does when the API contract changes.

## The coupling model

The SDK is coupled to the **Integrations API v1 contract**, not to platform releases. Most platform releases (UI work, unrelated features, internal fixes) do not touch the `/api/integrations/v1/*` surface and require nothing here. The SDK only needs attention when the contract itself changes: a new endpoint, a new field or parameter, a changed scope, or a changed error shape.

Two artifacts pin that coupling:

- `contract/integrations-v1.yaml`: a byte-identical snapshot of the OpenAPI document the server serves at `https://courses.trainingandetrackingsolutions.com/api/integrations/v1/openapi.yaml`.
- `tests/TeTS.Integrations.Tests/ContractParityTests.cs`: locks the SDK to that snapshot. Every operation in the contract must be accounted for, and every required schema property must exist on its mapped model with the right wire name.

Because the parity tests only see the snapshot, the snapshot itself can silently fall behind the live server. Closing that gap is the job of the drift check.

## The drift check

`.github/workflows/contract-drift.yml` runs each weekday morning (and on manual dispatch). It fetches the live contract, diffs it against the embedded snapshot, and:

- **On drift**: opens or updates a tracking issue labeled `contract-drift` containing the diff, and fails the run.
- **On match**: closes any open `contract-drift` issues.

The platform repo also has the mirror-image guard: PRs there that touch the OpenAPI document get an automatic reminder comment that the SDK needs a sync once the change ships.

## Responding to drift

1. Run `scripts/sync-contract.sh`. It refreshes `contract/integrations-v1.yaml` from the live server. Never hand-edit or annotate that file; the drift diff is on raw bytes.
2. Run `dotnet test`. The parity tests now tell you exactly what kind of change this is:
   - **Tests pass**: the change is documentation or metadata only (descriptions, examples, vendor extensions). Commit the refreshed contract. No release needed.
   - **`EveryContractOperationIsAccountedFor` fails**: the server added an endpoint. Wrap it (client method, models, tests), add it to `WrappedOperations`, and cut a **minor** release.
   - **`EveryRequiredSchemaPropertyExistsOnItsModel` fails**: a schema gained a required property. Add it to the mapped model and cut a **minor** release (or **patch** if it is serialization-only with no new public surface).
3. Update `CHANGELOG.md` and, if releasing, bump `<Version>` in `src/TeTS.Integrations/TeTS.Integrations.csproj` and push a matching `v*` tag. `release.yml` verifies the tag against the csproj version and publishes to NuGet via Trusted Publishing.

## Versioning policy

The package follows semver. `/v1` routes are stable for the lifetime of v1; the platform ships breaking API changes as a new version prefix, not in place. Mapping to SDK versions:

| Contract change | SDK action | Version bump |
| --- | --- | --- |
| Docs, examples, vendor extensions | Commit refreshed contract | none |
| New endpoint or new optional field | Wrap it | minor |
| New required property on an existing schema | Extend the model | minor (patch if no new public surface) |
| New API version prefix (`/v2`) | New major SDK effort, planned deliberately | major |

## Operational notes

- GitHub disables scheduled workflows in a repo after 60 days without commits. If this repo goes quiet, re-enable the drift check under the Actions tab or run it manually with `workflow_dispatch`; drift is also still caught by the platform-side PR guard.
- The drift check needs no secrets: the contract endpoint is public by design (it serves the API documentation).
- The smoke runner (`samples/TeTS.SmokeTest`) is the end-to-end complement: run it against staging with a real key when a sync involved actual surface changes.
