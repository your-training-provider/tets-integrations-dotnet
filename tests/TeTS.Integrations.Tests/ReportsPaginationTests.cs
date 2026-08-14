using System.Net;
using TeTS.Integrations;
using Xunit;

namespace TeTS.Integrations.Tests;

public class ReportsPaginationTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k" });
        return (client, handler);
    }

    private static string Page(string first, bool hasMore, string? next) => $$$"""
      {"from":"2026-01-01T00:00:00Z","to":"2026-01-31T00:00:00Z","count":1,
       "completions":[{"firstName":"{{{first}}}","lastName":"L","courseName":"C",
         "userId":"u1","completedDate":"2026-01-15T10:00:00Z"}],
       "pagination":{"limit":200,"hasMore":{{{(hasMore ? "true" : "false")}}},"nextCursor":{{{(next is null ? "null" : $"\"{next}\"")}}}}}
      """;

    [Fact]
    public async Task Page_SendsDateAndPagingParams()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        var report = await client.Reports.GetCompletionsPageAsync(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), cursor: "cur1", limit: 500);
        Assert.Equal(1, report.Count);
        var q = handler.Requests[0].Request.RequestUri!.Query;
        Assert.Contains("from=2026-01-01", q);
        Assert.Contains("to=2026-01-31", q);
        Assert.Contains("cursor=cur1", q);
        Assert.Contains("limit=500", q);
        Assert.EndsWith("/api/integrations/v1/reports/completions", handler.Requests[0].Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Enumerable_FollowsCursorUntilDone()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.OK, Page("B", true, "c3"))
               .Enqueue(HttpStatusCode.OK, Page("C", false, null));
        var names = new List<string>();
        await foreach (var record in client.Reports.GetCompletionsAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)))
            names.Add(record.FirstName);
        Assert.Equal(new[] { "A", "B", "C" }, names);
        Assert.Equal(3, handler.Requests.Count);
        Assert.DoesNotContain("cursor", handler.Requests[0].Request.RequestUri!.Query);
        Assert.Contains("cursor=c2", handler.Requests[1].Request.RequestUri!.Query);
        Assert.Contains("cursor=c3", handler.Requests[2].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task GetCompletionsPageAsync_ToBeforeFrom_ThrowsArgumentException_BeforeAnyRequest()
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Reports.GetCompletionsPageAsync(new DateTime(2026, 1, 31), new DateTime(2026, 1, 1)));
        Assert.Equal("to", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void GetCompletionsAsync_ToBeforeFrom_ThrowsArgumentException_BeforeAnyRequest()
    {
        var (client, handler) = Make();
        var ex = Assert.Throws<ArgumentException>(() =>
            client.Reports.GetCompletionsAsync(new DateTime(2026, 1, 31), new DateTime(2026, 1, 1)));
        Assert.Equal("to", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task GetCompletionsPageAsync_LimitOutOfRange_ThrowsArgumentOutOfRangeException_BeforeAnyRequest(int limit)
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Reports.GetCompletionsPageAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), limit: limit));
        Assert.Equal("limit", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void GetCompletionsAsync_LimitOutOfRange_ThrowsArgumentOutOfRangeException_BeforeAnyRequest(int limit)
    {
        var (client, handler) = Make();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.Reports.GetCompletionsAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), limit: limit));
        Assert.Equal("limit", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetCompletionsPageAsync_LimitInRangeBoundaries_DoesNotThrow()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null))
               .Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await client.Reports.GetCompletionsPageAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), limit: 1);
        await client.Reports.GetCompletionsPageAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), limit: 1000);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Enumerable_ServerReturnsSameCursorTwice_ThrowsAfterExactlyTwoRequests_PreservingYieldedRecords()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.OK, Page("B", true, "c2"));
        var names = new List<string>();
        var ex = await Assert.ThrowsAsync<TetsApiException>(async () =>
        {
            await foreach (var record in client.Reports.GetCompletionsAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)))
                names.Add(record.FirstName);
        });
        Assert.Equal(TetsErrorCode.PaginationStalled, ex.Code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "A", "B" }, names);
    }

    [Fact]
    public async Task Enumerable_MidPaginationFailure_PreservesFirstPageRecords_ThenThrowsOnSecondPull()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = 0 });
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.InternalServerError,
                   """{"error":"boom","code":"INTERNAL_ERROR","requestId":"r"}""");
        var names = new List<string>();
        var ex = await Assert.ThrowsAsync<TetsApiException>(async () =>
        {
            await foreach (var record in client.Reports.GetCompletionsAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31)))
                names.Add(record.FirstName);
        });
        Assert.Equal(TetsErrorCode.InternalError, ex.Code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(names);
        Assert.Equal("A", names[0]);
    }
}
