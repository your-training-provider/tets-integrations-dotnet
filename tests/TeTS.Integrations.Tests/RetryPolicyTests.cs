using System.Diagnostics;
using System.Net;
using TeTS.Integrations;
using Xunit;

namespace TeTS.Integrations.Tests;

public class RetryPolicyTests
{
    private const string PingBody = """
      {"ok":true,"integrationSlug":"acme","connectionId":"c1","connectionLabel":"Acme",
       "organizationTenantId":"t1","orgId":"o1","requestId":"req_ping"}
      """;
    private const string RateLimitedBody = """{"error":"Slow down.","code":"RATE_LIMITED","requestId":"req_429"}""";

    private static (TetsIntegrationsClient, TestHttpHandler) Make(int maxRetries)
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = maxRetries });
        return (client, handler);
    }

    // This file proves end-to-end retry POLICY (which statuses/codes retry, backoff honored, exhaustion);
    // TransportEdgeCaseTests covers Retry-After header PARSING/clamping edge cases.
    [Fact]
    public async Task RetriesOn429_ThenSucceeds_HonoringRetryAfter()
    {
        var (client, handler) = Make(maxRetries: 2);
        handler.Enqueue((HttpStatusCode)429, RateLimitedBody, r => r.Headers.Add("Retry-After", "1"))
               .Enqueue(HttpStatusCode.OK, PingBody);
        var sw = Stopwatch.StartNew();
        var ping = await client.PingAsync();
        sw.Stop();
        Assert.True(ping.Ok);
        Assert.Equal(2, handler.Requests.Count);
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(0.9), $"waited only {sw.Elapsed}");
    }

    [Fact]
    public async Task ExhaustsRetries_ThenThrowsLastError()
    {
        var (client, handler) = Make(maxRetries: 1);
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, """{"error":"down","code":"FEATURE_DISABLED","requestId":"r1"}""")
               .Enqueue(HttpStatusCode.ServiceUnavailable, """{"error":"down","code":"FEATURE_DISABLED","requestId":"r2"}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(TetsErrorCode.FeatureDisabled, ex.Code);
        Assert.Equal("r2", ex.RequestId);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DoesNotRetryClientErrors()
    {
        var (client, handler) = Make(maxRetries: 3);
        handler.Enqueue(HttpStatusCode.NotFound,
            """{"error":"nope","code":"INTEGRATION_USER_NOT_FOUND","requestId":"r"}""");
        await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Retries409IdempotencyInFlight_ButNotOther409s()
    {
        var (client, handler) = Make(maxRetries: 1);
        handler.Enqueue(HttpStatusCode.Conflict,
                """{"error":"busy","code":"IDEMPOTENCY_REQUEST_IN_FLIGHT","requestId":"r1"}""")
               .Enqueue(HttpStatusCode.OK, PingBody);
        await client.PingAsync();
        Assert.Equal(2, handler.Requests.Count);

        var (client2, handler2) = Make(maxRetries: 1);
        handler2.Enqueue(HttpStatusCode.Conflict,
            """{"error":"taken","code":"USERNAME_TAKEN","requestId":"r2"}""");
        await Assert.ThrowsAsync<TetsApiException>(() => client2.PingAsync());
        Assert.Single(handler2.Requests);
    }
}
