using System.Net;
using TeTS.Integrations;
using Xunit;

namespace TeTS.Integrations.Tests;

public class TransportTests
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

    [Fact]
    public async Task SendsApiKeyHeader_AndParsesResponse()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, PingBody);
        var ping = await client.PingAsync();
        Assert.True(ping.Ok);
        Assert.Equal("acme", ping.IntegrationSlug);
        var req = Assert.Single(handler.Requests).Request;
        Assert.Equal("https://api.example.com/api/integrations/v1/ping", req.RequestUri!.ToString());
        Assert.Equal("key-123", Assert.Single(req.Headers.GetValues("x-api-key")));
        Assert.False(req.Headers.Contains("X-Integration-Tenant-Id"));
    }

    [Fact]
    public async Task SendsTenantHeader_WhenConfigured()
    {
        var (client, handler) = Make(o => o.OrganizationTenantId = "tenant-1");
        handler.Enqueue(HttpStatusCode.OK, PingBody);
        await client.PingAsync();
        Assert.Equal("tenant-1",
            Assert.Single(Assert.Single(handler.Requests).Request.Headers.GetValues("X-Integration-Tenant-Id")));
    }

    [Fact]
    public async Task ErrorEnvelope_BecomesTetsApiException()
    {
        var (client, handler) = Make(o => o.MaxRetries = 0);
        handler.Enqueue(HttpStatusCode.Unauthorized,
            """{"error":"Invalid API key.","code":"UNAUTHORIZED","requestId":"req_err"}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(TetsErrorCode.Unauthorized, ex.Code);
        Assert.Equal("req_err", ex.RequestId);
    }

    [Fact]
    public async Task UnparseableErrorBody_FallsBackToHeaderRequestId()
    {
        var (client, handler) = Make(o => o.MaxRetries = 0);
        handler.Enqueue(HttpStatusCode.InternalServerError, "<html>gateway broke</html>",
            r => r.Headers.Add("X-Request-Id", "req_hdr"));
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.PingAsync());
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
        Assert.Equal("req_hdr", ex.RequestId);
        Assert.Contains("gateway broke", ex.RawBody);
    }

    [Fact]
    public async Task CapturesRateLimitHeaders()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, PingBody, r =>
        {
            r.Headers.Add("X-RateLimit-Limit", "100");
            r.Headers.Add("X-RateLimit-Remaining", "97");
            r.Headers.Add("X-RateLimit-Reset", "1786500000");
        });
        await client.PingAsync();
        Assert.NotNull(client.LastRateLimit);
        Assert.Equal(100, client.LastRateLimit!.Limit);
        Assert.Equal(97, client.LastRateLimit.Remaining);
        Assert.Equal(1786500000, client.LastRateLimit.ResetEpochSeconds);
    }

    [Fact]
    public void MissingBaseUrlOrApiKey_ThrowsOnConstruction()
    {
        Assert.Throws<ArgumentException>(() =>
            new TetsIntegrationsClient(new TetsOptions { BaseUrl = "", ApiKey = "k" }));
        Assert.Throws<ArgumentException>(() =>
            new TetsIntegrationsClient(new TetsOptions { BaseUrl = "https://x", ApiKey = " " }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveTimeout_ThrowsOnConstruction(int seconds)
    {
        var ex = Assert.Throws<ArgumentException>(() => new TetsIntegrationsClient(new TetsOptions
        { BaseUrl = "https://x.example.com", ApiKey = "k", Timeout = TimeSpan.FromSeconds(seconds) }));
        Assert.Contains("TetsOptions.Timeout", ex.Message);
    }
}
