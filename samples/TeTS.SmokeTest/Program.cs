using TeTS.Integrations;
using TeTS.Integrations.Models;
using TeTS.Integrations.Sso;

// TeTS Integrations API v1 smoke test — the partner onboarding/UAT checklist.
// Creates ONE disposable test user, then deactivates it. Point at staging, not prod.
//
// Required env: TETS_BASE_URL, TETS_API_KEY
// Optional env: TETS_SSO_SECRET, TETS_INTEGRATION_SLUG, TETS_TENANT_ID, TETS_COURSE_ID,
//               TETS_GROUP_ID (group to place the test user in; without it the server uses the
//               connection's configured default group, or rejects the create if none is configured)

static string? Env(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

var baseUrl = Env("TETS_BASE_URL") ?? Fail("TETS_BASE_URL is required.");
var apiKey = Env("TETS_API_KEY") ?? Fail("TETS_API_KEY is required.");

// TetsIntegrationsClient owns an HttpClient and is IDisposable; `using var` here disposes it on
// every exit path below (normal completion, an unhandled exception, or Environment.Exit is not
// reached in that last case, but the CLR still runs finalizers/dispose on process exit for the
// paths that do return normally).
using var client = CreateClient();

var failures = 0;
var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
var userName = $"tets.smoke.{stamp}";
var externalId = $"smoke-{Guid.NewGuid():N}";

// 1. ping — API key + tenant scope
await Step("1. ping (auth + scope)", async () =>
{
    var ping = await client.PingAsync();
    return $"integration={ping.IntegrationSlug} connection={ping.ConnectionLabel} tenant={ping.OrganizationTenantId} requestId={ping.RequestId}";
});

// 2. username availability
await Step("2. users/exists", async () =>
{
    var exists = await client.Users.CheckExistsAsync(userName);
    return $"exists={exists.Exists} linkedToIntegration={exists.LinkedToIntegration}";
});

// 3. create user
await Step("3. create user", async () =>
{
    var user = await client.Users.CreateAsync(new CreateUserRequest
    {
        ExternalId = externalId, UserName = userName,
        FirstName = "Smoke", LastName = "Test", Email = $"{userName}@example.invalid",
        GroupIds = Env("TETS_GROUP_ID") is { } groupId ? new List<string> { groupId } : null,
    });
    return $"userId={user.UserId} status={user.Status} groups=[{string.Join(",", user.GroupIds)}]";
});

// 4. fetch it back by externalId
await Step("4. get by externalId", async () =>
{
    var user = await client.Users.GetByExternalIdAsync(externalId);
    return $"found {user.FirstName} {user.LastName} status={user.Status}";
});

// 5. list users (first page of the org roster; stop after 25 — call shape is the test)
await Step("5. list users (first page)", async () =>
{
    var seen = 0;
    UserListItem? first = null;
    await foreach (var user in client.Users.ListAsync())
    {
        first ??= user;
        if (++seen >= 25) break;
    }
    return first is null
        ? "0 users visible to this connection"
        : $"saw {seen} user(s); first: userName={first.UserName ?? "(null)"} externalId={(first.ExternalId is null ? "null (not yet linked)" : "set")}";
});

// 6. SSO launch URL (printed for manual browser verification)
await Step("6. SSO launch URL", () =>
{
    if (Env("TETS_SSO_SECRET") is null || Env("TETS_INTEGRATION_SLUG") is null)
        return Task.FromResult("SKIPPED (set TETS_SSO_SECRET and TETS_INTEGRATION_SLUG to enable)");
    var url = client.Sso.BuildLaunchUrl(new SsoLaunchRequest
    {
        UserName = userName, Identification = externalId, CourseId = Env("TETS_COURSE_ID"),
        OrganizationTenantId = Env("TETS_TENANT_ID"),
    });
    return Task.FromResult($"open in a browser to verify:\n   {url}");
});

// 7. completions polling (last 30 days; the fresh smoke user has none — call shape is the test)
await Step("7. completions report", async () =>
{
    var count = 0;
    await foreach (var _ in client.Reports.GetCompletionsAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow))
        count++;
    return $"{count} completion(s) in the last 30 days (pagination followed automatically)";
});

// 8. deactivate the smoke user
await Step("8. deactivate user", async () =>
{
    var result = await client.Users.DeactivateAsync(externalId);
    return $"status={result.Status}";
});

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL STEPS PASSED" : $"{failures} STEP(S) FAILED");
return failures == 0 ? 0 : 1;

async Task Step(string title, Func<Task<string>> action)
{
    Console.WriteLine($"\n=== {title} ===");
    try
    {
        Console.WriteLine($"   OK: {await action()}");
    }
    catch (TetsApiException ex)
    {
        failures++;
        Console.WriteLine($"   FAILED: {ex.Code} — {ex.Message}");
        Console.WriteLine($"   (send TeTS this requestId: {ex.RequestId ?? "n/a"})");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"   FAILED: {ex.GetType().Name} — {ex.Message}");
    }
}

// Constructing the client validates BaseUrl/ApiKey synchronously and throws ArgumentException on
// bad input (e.g. a malformed TETS_BASE_URL). Missing-env-var checks above already exit(2) before
// we get here, so this only guards against a present-but-invalid value.
TetsIntegrationsClient CreateClient()
{
    try
    {
        return new TetsIntegrationsClient(new TetsOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            OrganizationTenantId = Env("TETS_TENANT_ID"),
            IntegrationSlug = Env("TETS_INTEGRATION_SLUG"),
            SsoSecret = Env("TETS_SSO_SECRET"),
        });
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.Exit(2);
        throw; // unreachable — Environment.Exit terminates the process above.
    }
}

static string Fail(string message)
{
    Console.Error.WriteLine(message);
    Environment.Exit(2);
    return "";
}
