using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class CatalogListPaginationTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k" });
        return (client, handler);
    }

    private static string Page(string first, bool hasMore, string? next) => $$$"""
      {"items":[{"productId":"{{{first}}}","productType":"course","title":"Course {{{first}}}","code":"SKU-{{{first}}}",
        "categoryNames":["Compliance"],"certValidityDays":365,"updatedAt":"2026-08-01T12:00:00Z",
        "legacyCourseId":42,"legacyProgramId":null,"renewOnly":false,"programCourses":null}],
       "pagination":{"limit":200,"hasMore":{{{(hasMore ? "true" : "false")}}},"nextCursor":{{{(next is null ? "null" : $"\"{next}\"")}}}}}
      """;

    [Fact]
    public async Task Enumerable_FollowsCursorUntilDone()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.OK, Page("B", true, "c3"))
               .Enqueue(HttpStatusCode.OK, Page("C", false, null));
        var ids = new List<string>();
        await foreach (var item in client.Catalog.ListAsync())
            ids.Add(item.ProductId);
        Assert.Equal(new[] { "A", "B", "C" }, ids);
        Assert.Equal(3, handler.Requests.Count);
        Assert.DoesNotContain("cursor", handler.Requests[0].Request.RequestUri!.Query);
        Assert.Contains("cursor=c2", handler.Requests[1].Request.RequestUri!.Query);
        Assert.Contains("cursor=c3", handler.Requests[2].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task SinglePage_MakesExactlyOneRequest_ToTheCatalogPath()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        var items = new List<CatalogItem>();
        await foreach (var item in client.Catalog.ListAsync())
            items.Add(item);
        var recorded = Assert.Single(handler.Requests);
        Assert.EndsWith("/api/integrations/v1/catalog", recorded.Request.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, recorded.Request.Method);
        var i = Assert.Single(items);
        Assert.Equal("Course A", i.Title);
        Assert.Equal("SKU-A", i.Code);
        Assert.Equal(new[] { "Compliance" }, i.CategoryNames);
    }

    [Fact]
    public async Task CourseRow_AllFieldsDeserialize_WithNullLegacyProgramIdAndNullProgramCourses()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """
          {"items":[{"productId":"p-course","productType":"course","title":"HIPAA Basics","code":"HIP-1",
            "categoryNames":["Compliance","Healthcare"],"certValidityDays":365,
            "updatedAt":"2026-08-01T12:34:56Z","legacyCourseId":4181,"legacyProgramId":null,
            "renewOnly":false,"programCourses":null}],
           "pagination":{"limit":200,"hasMore":false,"nextCursor":null}}
          """);
        var items = new List<CatalogItem>();
        await foreach (var item in client.Catalog.ListAsync())
            items.Add(item);
        var i = Assert.Single(items);
        Assert.Equal("p-course", i.ProductId);
        Assert.Equal("course", i.ProductType);
        Assert.Equal("HIPAA Basics", i.Title);
        Assert.Equal("HIP-1", i.Code);
        Assert.Equal(new[] { "Compliance", "Healthcare" }, i.CategoryNames);
        Assert.Equal(365, i.CertValidityDays);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 12, 34, 56, TimeSpan.Zero), i.UpdatedAt);
        Assert.Equal(4181, i.LegacyCourseId);
        Assert.Null(i.LegacyProgramId);
        Assert.False(i.RenewOnly);
        Assert.Null(i.ProgramCourses);   // non-program products carry no child list
    }

    [Fact]
    public async Task ProgramRow_ProgramCoursesPopulatedInOrder_AndRenewOnlyCourseDeserializes()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """
          {"items":[
            {"productId":"p-prog","productType":"program","title":"Orientation Pathway","code":null,
             "categoryNames":[],"certValidityDays":null,"updatedAt":"2026-07-15T00:00:00Z",
             "legacyCourseId":null,"legacyProgramId":77,"renewOnly":false,
             "programCourses":[
               {"productId":"child-1","sortOrder":1,"isRequired":true},
               {"productId":"child-2","sortOrder":2,"isRequired":false}]},
            {"productId":"p-old","productType":"course","title":"OSHA (2019 edition)","code":null,
             "categoryNames":["Safety"],"certValidityDays":null,"updatedAt":"2026-07-15T00:00:00Z",
             "legacyCourseId":901,"legacyProgramId":null,"renewOnly":true,"programCourses":null}],
           "pagination":{"limit":200,"hasMore":false,"nextCursor":null}}
          """);
        var items = new List<CatalogItem>();
        await foreach (var item in client.Catalog.ListAsync())
            items.Add(item);
        Assert.Equal(2, items.Count);

        var program = items[0];
        Assert.Equal("program", program.ProductType);
        Assert.Null(program.Code);
        Assert.Empty(program.CategoryNames);
        Assert.Null(program.CertValidityDays);
        Assert.Null(program.LegacyCourseId);
        Assert.Equal(77, program.LegacyProgramId);
        Assert.NotNull(program.ProgramCourses);
        Assert.Equal(new[] { "child-1", "child-2" }, program.ProgramCourses!.Select(c => c.ProductId));
        Assert.Equal(new[] { 1, 2 }, program.ProgramCourses.Select(c => c.SortOrder));
        Assert.True(program.ProgramCourses[0].IsRequired);
        Assert.False(program.ProgramCourses[1].IsRequired);   // elective pool member

        var renewOnly = items[1];
        Assert.True(renewOnly.RenewOnly);   // superseded edition — historical interpretation only
        Assert.Equal(901, renewOnly.LegacyCourseId);
        Assert.Null(renewOnly.ProgramCourses);
    }

    [Fact]
    public async Task PageSize_LandsInQueryString()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Catalog.ListAsync(new ListCatalogOptions { PageSize = 500 })) { }
        Assert.Contains("limit=500", handler.Requests[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task TenantOverrideOnOptions_SendsTenantHeader()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Catalog.ListAsync(new ListCatalogOptions
        { OrganizationTenantId = "tenant-1" })) { }
        Assert.Equal("tenant-1",
            Assert.Single(handler.Requests[0].Request.Headers.GetValues("X-Integration-Tenant-Id")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void ListAsync_PageSizeOutOfRange_ThrowsArgumentOutOfRangeException_BeforeAnyRequest(int pageSize)
    {
        var (client, handler) = Make();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.Catalog.ListAsync(new ListCatalogOptions { PageSize = pageSize }));
        Assert.Equal("options", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ListAsync_PageSizeInRangeBoundaries_DoesNotThrow()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null))
               .Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Catalog.ListAsync(new ListCatalogOptions { PageSize = 1 })) { }
        await foreach (var _ in client.Catalog.ListAsync(new ListCatalogOptions { PageSize = 1000 })) { }
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("limit=1", handler.Requests[0].Request.RequestUri!.Query);
        Assert.Contains("limit=1000", handler.Requests[1].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task Enumerable_ServerReturnsSameCursorTwice_ThrowsAfterExactlyTwoRequests_PreservingYieldedItems()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.OK, Page("B", true, "c2"));
        var ids = new List<string>();
        var ex = await Assert.ThrowsAsync<TetsApiException>(async () =>
        {
            await foreach (var item in client.Catalog.ListAsync())
                ids.Add(item.ProductId);
        });
        Assert.Equal(TetsErrorCode.PaginationStalled, ex.Code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "A", "B" }, ids);
    }

    [Fact]
    public async Task ErrorEnvelope_SurfacesAsTetsApiException_WithCodeAndRequestId()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Forbidden,
            """{"error":"Connection forbidden.","code":"INTEGRATION_CONNECTION_FORBIDDEN","requestId":"req_403"}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(async () =>
        {
            await foreach (var _ in client.Catalog.ListAsync()) { }
        });
        Assert.Equal(TetsErrorCode.IntegrationConnectionForbidden, ex.Code);
        Assert.Equal("req_403", ex.RequestId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Enumerable_MidPaginationFailure_PreservesFirstPageItems_ThenThrowsOnSecondPull()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = 0 });
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.InternalServerError,
                   """{"error":"boom","code":"INTERNAL_ERROR","requestId":"r"}""");
        var ids = new List<string>();
        var ex = await Assert.ThrowsAsync<TetsApiException>(async () =>
        {
            await foreach (var item in client.Catalog.ListAsync())
                ids.Add(item.ProductId);
        });
        Assert.Equal(TetsErrorCode.InternalError, ex.Code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "A" }, ids);
    }
}
