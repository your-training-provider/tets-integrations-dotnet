using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class UsersHireDatesTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make(string? slug = "acme")
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = 1, IntegrationSlug = slug });
        return (client, handler);
    }

    private const string ResultBody = """
      {"total":2,"updated":1,"skipped":1,"results":[
        {"externalId":"E-1","email":null,"status":"updated","matchedBy":"external_id","userId":"u-1"},
        {"externalId":null,"email":"nobody@example.com","status":"skipped","reason":"No learner matches this row."}]}
      """;

    [Fact]
    public async Task SetHireDates_PostsToSlugRoute_WithCalendarDates()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, ResultBody);

        var result = await client.Users.SetHireDatesAsync(new[]
        {
            new HireDateUpdate { ExternalId = "E-1", HireDate = new DateTime(2026, 3, 9, 23, 30, 0, DateTimeKind.Local) },
            new HireDateUpdate { Email = "nobody@example.com", HireDate = new DateTime(2026, 12, 1) },
        });

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal("external_id", result.Results[0].MatchedBy);
        Assert.Equal("u-1", result.Results[0].UserId);
        Assert.Equal("skipped", result.Results[1].Status);
        Assert.Equal("No learner matches this row.", result.Results[1].Reason);

        var recorded = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, recorded.Request.Method);
        Assert.Equal("/api/integrations/acme/hire-dates", recorded.Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"employees\":[", recorded.Body);
        // Only the calendar date is sent; the time of day and Kind never shift it.
        Assert.Contains("\"hireDate\":\"2026-03-09\"", recorded.Body);
        Assert.Contains("\"hireDate\":\"2026-12-01\"", recorded.Body);
        Assert.DoesNotContain("\"email\":null", recorded.Body);
    }

    [Fact]
    public async Task SetHireDates_EscapesSlug()
    {
        var (client, handler) = Make(slug: "a b");
        handler.Enqueue(HttpStatusCode.OK, """{"total":1,"updated":1,"skipped":0,"results":[]}""");
        await client.Users.SetHireDatesAsync(new[] { new HireDateUpdate { ExternalId = "E-1", HireDate = new DateTime(2026, 1, 2) } });
        Assert.Equal("/api/integrations/a%20b/hire-dates", handler.Requests[0].Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void SetHireDates_WithoutSlug_ThrowsInvalidOperationException_BeforeAnyRequest()
    {
        var (client, handler) = Make(slug: null);
        Assert.Throws<InvalidOperationException>(() =>
        { _ = client.Users.SetHireDatesAsync(new[] { new HireDateUpdate { ExternalId = "E-1", HireDate = DateTime.Today } }); });
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void SetHireDates_InvalidRows_ThrowBeforeAnyRequest()
    {
        var (client, handler) = Make();
        Assert.Throws<ArgumentNullException>(() => { _ = client.Users.SetHireDatesAsync(null!); });
        Assert.Throws<ArgumentException>(() => { _ = client.Users.SetHireDatesAsync(Array.Empty<HireDateUpdate>()); });
        Assert.Throws<ArgumentException>(() => { _ = client.Users.SetHireDatesAsync(new HireDateUpdate[] { null! }); });
        Assert.Throws<ArgumentException>(() =>
        { _ = client.Users.SetHireDatesAsync(new[] { new HireDateUpdate { HireDate = DateTime.Today } }); });
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SetHireDates_InsufficientScope_SurfacesTypedCode()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Forbidden, """{"error":"Missing scope users:write.","code":"INSUFFICIENT_SCOPE","requestId":"req_9"}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() =>
            client.Users.SetHireDatesAsync(new[] { new HireDateUpdate { ExternalId = "E-1", HireDate = DateTime.Today } }));
        Assert.Equal(TetsErrorCode.InsufficientScope, ex.Code);
    }
}
