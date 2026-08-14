using System.Diagnostics;
using System.Net;
using System.Text.Json;
using TeTS.Integrations;
using TeTS.Integrations.Http;
using Xunit;

namespace TeTS.Integrations.Tests;

/// <summary>Edge-case coverage from the Task 3 hardening pass: safe 2xx body handling, clamped
/// typed Retry-After, IDisposable ownership, read-only JSON options, and the small fixes alongside them.</summary>
public class TransportEdgeCaseTests
{
    private const string PingBody = """
      {"ok":true,"integrationSlug":"acme","connectionId":"c1","connectionLabel":"Acme",
       "organizationTenantId":"t1","orgId":"o1","requestId":"req_ping"}
      """;

    private static (TetsIntegrationsClient client, TestHttpHandler handler) Make(
        Action<TetsOptions>? configure = null)
    {
        var handler = new TestHttpHandler();
        var options = new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "key-123" };
        configure?.Invoke(options);
        return (new TetsIntegrationsClient(new HttpClient(handler), options), handler);
    }

    // ---- C1: safe 2xx body handling --------------------------------------------

    [Fact]
    public async Task SuccessResponse_EmptyBody_ThrowsTetsApiException_NotJsonException()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, "");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
        Assert.Equal("", ex.RawBody);
    }

    [Fact]
    public async Task SuccessResponse_NonJsonBody_ThrowsTetsApiException_WithInnerJsonException()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, "<html>not json</html>");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
        Assert.Contains("not json", ex.RawBody);
        Assert.IsType<JsonException>(ex.InnerException);
    }

    // ---- C2 + I1: typed, clamped Retry-After -----------------------------------

    [Fact]
    public async Task RetryAfterNegative_IsRejectedByParser_FallsBackToBackoff()
    {
        // "-5" isn't a valid delta-seconds token per RFC 7231, so the typed RetryConditionHeaderValue
        // parser rejects it outright (RetryAfterDelay sees no usable header) — ClampRetryAfter's [0,60]
        // floor never even runs here. The outcome is the same (a fast retry) but via normal exponential
        // backoff, not the clamp.
        var (client, handler) = Make(); // default MaxRetries = 3
        handler.Enqueue(HttpStatusCode.TooManyRequests, "{}",
            r => r.Headers.TryAddWithoutValidation("Retry-After", "-5"));
        handler.Enqueue(HttpStatusCode.OK, PingBody);

        var sw = Stopwatch.StartNew();
        var ping = await client.PingAsync();
        sw.Stop();

        Assert.True(ping.Ok);
        // Generous upper bound: the point of this assertion is only that we fell back to normal
        // (short) backoff instead of sleeping for a long/invalid Retry-After — not to pin the
        // exact jittered delay. First-attempt backoff is 0.5s + up to 0.25s jitter, which can graze
        // 1s on a loaded CI runner, so a tight bound flakes without weakening what's being proven.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"expected a fast retry, took {sw.Elapsed}");
    }

    [Fact]
    public async Task RetryAfterSmallPositive_DelaysApproximatelyThatLong()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.TooManyRequests, "{}",
            r => r.Headers.TryAddWithoutValidation("Retry-After", "2"));
        handler.Enqueue(HttpStatusCode.OK, PingBody);

        var sw = Stopwatch.StartNew();
        var ping = await client.PingAsync();
        sw.Stop();

        Assert.True(ping.Ok);
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(1.9), $"expected ~2s delay, took {sw.Elapsed}");
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(3600, 60)]
    [InlineData(2, 2)]
    public void ClampRetryAfter_ClampsToZeroToSixtySecondsRange(double inputSeconds, double expectedSeconds)
    {
        var clamped = ApiConnection.ClampRetryAfter(TimeSpan.FromSeconds(inputSeconds));
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), clamped);
    }

    // ---- I2: IDisposable ownership ----------------------------------------------

    [Fact]
    public async Task Dispose_WithInjectedHttpClient_DoesNotDisposeIt()
    {
        var handler = new TestHttpHandler();
        var httpClient = new HttpClient(handler);
        var options = new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "key-123" };

        var client = new TetsIntegrationsClient(httpClient, options);
        client.Dispose();

        handler.Enqueue(HttpStatusCode.OK, PingBody);
        var client2 = new TetsIntegrationsClient(httpClient, options);
        var ping = await client2.PingAsync();
        Assert.True(ping.Ok);
    }

    // ---- I3: read-only shared JSON options ---------------------------------------

    [Fact]
    public void TetsJsonOptions_IsReadOnly()
    {
        Assert.True(TetsJson.Options.IsReadOnly);
    }

    // ---- Minor: status-code fallback for unparseable 429 -------------------------

    [Fact]
    public async Task RateLimitStatus_WithUnparseableBody_MapsToRateLimited()
    {
        var (client, handler) = Make(o => o.MaxRetries = 0);
        handler.Enqueue(HttpStatusCode.TooManyRequests, "<html>slow down</html>");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(TetsErrorCode.RateLimited, ex.Code);
    }

    // ---- Minor: BaseUrl must be an absolute http/https URI -----------------------

    [Fact]
    public void InvalidBaseUrl_ThrowsOnConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TetsIntegrationsClient(new TetsOptions { BaseUrl = "api.example.com", ApiKey = "k" }));
        Assert.Contains("BaseUrl", ex.Message);
    }

    // ---- Minor: default Accept / User-Agent headers -------------------------------

    [Fact]
    public async Task Requests_CarryAcceptAndUserAgentHeaders()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, PingBody);
        await client.PingAsync();
        var req = Assert.Single(handler.Requests).Request;
        Assert.Equal("application/json", Assert.Single(req.Headers.GetValues("Accept")));
        var userAgent = Assert.Single(req.Headers.GetValues("User-Agent"));
        Assert.StartsWith("TeTS.Integrations/", userAgent);
        // IncludeSourceRevisionInInformationalVersion=false must keep the commit SHA out of this token.
        Assert.DoesNotContain("+", userAgent);
    }
}
