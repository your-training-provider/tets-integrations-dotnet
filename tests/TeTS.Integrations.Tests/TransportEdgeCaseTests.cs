using System.Diagnostics;
using System.Net;
using System.Text.Json;
using TeTS.Integrations;
using TeTS.Integrations.Http;
using Xunit;

namespace TeTS.Integrations.Tests;

/// <summary>Transport edge cases: empty and non-JSON response bodies, Retry-After handling
/// (typed parsing and clamping), HttpClient disposal ownership, and shared serializer immutability.</summary>
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

    // ---- Safe 2xx body handling: empty or non-JSON success bodies ---------------

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

    // ---- Typed, clamped Retry-After handling ------------------------------------

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

    // ---- HttpClient disposal ownership -------------------------------------------

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

    // ---- Read-only shared JSON serializer options ---------------------------------

    [Fact]
    public void TetsJsonOptions_IsReadOnly()
    {
        Assert.True(TetsJson.Options.IsReadOnly);
    }

    // ---- Status-code fallback for unparseable 429 ---------------------------------

    [Fact]
    public async Task RateLimitStatus_WithUnparseableBody_MapsToRateLimited()
    {
        var (client, handler) = Make(o => o.MaxRetries = 0);
        handler.Enqueue(HttpStatusCode.TooManyRequests, "<html>slow down</html>");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(TetsErrorCode.RateLimited, ex.Code);
    }

    // ---- BaseUrl validation: absolute URI, https except loopback ------------------

    [Fact]
    public void InvalidBaseUrl_ThrowsOnConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TetsIntegrationsClient(new TetsOptions { BaseUrl = "api.example.com", ApiKey = "k" }));
        Assert.Contains("BaseUrl", ex.Message);
    }

    [Theory]
    [InlineData("https://api.example.com")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://[::1]:3000")]
    public void HttpsOrLoopbackHttpBaseUrl_IsAccepted(string baseUrl)
    {
        using var client = new TetsIntegrationsClient(new TetsOptions { BaseUrl = baseUrl, ApiKey = "k" });
    }

    [Fact]
    public void HttpBaseUrlWithNonLoopbackHost_ThrowsOnConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TetsIntegrationsClient(new TetsOptions { BaseUrl = "http://api.example.com", ApiKey = "k" }));
        Assert.Contains("BaseUrl must use https; http is allowed only for localhost development", ex.Message);
    }

    // ---- RawBody retention cap on TetsApiException --------------------------------

    [Fact]
    public async Task ErrorBodyLargerThan64KiB_RawBodyIsTruncatedWithMarker()
    {
        var (client, handler) = Make(o => o.MaxRetries = 0);
        var huge = new string('x', 100_000);
        handler.Enqueue(HttpStatusCode.BadRequest, huge);
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.EndsWith("\n...[truncated by SDK]", ex.RawBody);
        Assert.Equal(64 * 1024 + "\n...[truncated by SDK]".Length, ex.RawBody!.Length);
    }

    [Fact]
    public async Task SmallErrorBody_RawBodyIsRetainedUnchanged()
    {
        var (client, handler) = Make(o => o.MaxRetries = 0);
        const string body = """{"error":"nope","code":"VALIDATION_FAILED"}""";
        handler.Enqueue(HttpStatusCode.BadRequest, body);
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(body, ex.RawBody);
    }

    // ---- Default Accept / User-Agent headers --------------------------------------

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
