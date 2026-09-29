using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class GroupsListPaginationTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k" });
        return (client, handler);
    }

    private static string Page(string first, bool hasMore, string? next) => $$$"""
      {"items":[{"groupId":"{{{first}}}","name":"Group {{{first}}}","parentGroupId":"root-1",
        "isOrganizationRoot":false,"acceptsMembers":true,"legacyGroupId":"2291",
        "createdAt":"2026-05-02T12:00:00Z","updatedAt":"2026-08-01T12:00:00Z"}],
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
        await foreach (var group in client.Groups.ListAsync())
            ids.Add(group.GroupId);
        Assert.Equal(new[] { "A", "B", "C" }, ids);
        Assert.Equal(3, handler.Requests.Count);
        Assert.DoesNotContain("cursor", handler.Requests[0].Request.RequestUri!.Query);
        Assert.Contains("cursor=c2", handler.Requests[1].Request.RequestUri!.Query);
        Assert.Contains("cursor=c3", handler.Requests[2].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task SinglePage_MakesExactlyOneRequest_ToTheGroupsPath()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        var groups = new List<GroupItem>();
        await foreach (var group in client.Groups.ListAsync())
            groups.Add(group);
        var recorded = Assert.Single(handler.Requests);
        Assert.EndsWith("/api/integrations/v1/groups", recorded.Request.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, recorded.Request.Method);
        Assert.Equal("Group A", Assert.Single(groups).Name);
    }

    [Fact]
    public async Task RootAndMigratedAndNativeRows_AllFieldsDeserialize()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """
          {"items":[
            {"groupId":"f65db270-8123-4035-8df9-92f785167abf","name":"Example Org","parentGroupId":null,
             "isOrganizationRoot":true,"acceptsMembers":false,"legacyGroupId":"2290",
             "createdAt":"2026-05-02T12:34:56Z","updatedAt":"2026-09-01T09:00:00Z"},
            {"groupId":"79bb7f9b-4f38-4d87-a718-8a4b6ebbe748","name":"Example Org Staff",
             "parentGroupId":"f65db270-8123-4035-8df9-92f785167abf",
             "isOrganizationRoot":false,"acceptsMembers":true,"legacyGroupId":"2291",
             "createdAt":"2026-05-02T12:34:56Z","updatedAt":"2026-09-01T09:00:00Z"},
            {"groupId":"0c3dcd8a-6b79-4a1a-b138-47f61896eb4c","name":"Night Shift",
             "parentGroupId":"79bb7f9b-4f38-4d87-a718-8a4b6ebbe748",
             "isOrganizationRoot":false,"acceptsMembers":true,"legacyGroupId":null,
             "createdAt":"2026-09-20T08:00:00Z","updatedAt":"2026-09-20T08:00:00Z"}],
           "pagination":{"limit":200,"hasMore":false,"nextCursor":null}}
          """);
        var groups = new List<GroupItem>();
        await foreach (var group in client.Groups.ListAsync())
            groups.Add(group);
        Assert.Equal(3, groups.Count);

        var root = groups[0];
        Assert.Equal("f65db270-8123-4035-8df9-92f785167abf", root.GroupId);
        Assert.Equal("Example Org", root.Name);
        Assert.Null(root.ParentGroupId);
        Assert.True(root.IsOrganizationRoot);
        Assert.False(root.AcceptsMembers);   // record-only: membership adds reject the root
        Assert.Equal("2290", root.LegacyGroupId);
        Assert.Equal(new DateTimeOffset(2026, 5, 2, 12, 34, 56, TimeSpan.Zero), root.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), root.UpdatedAt);

        var migrated = groups[1];
        Assert.Equal(root.GroupId, migrated.ParentGroupId);
        Assert.False(migrated.IsOrganizationRoot);
        Assert.True(migrated.AcceptsMembers);
        Assert.Equal("2291", migrated.LegacyGroupId);

        var native = groups[2];
        Assert.Equal(migrated.GroupId, native.ParentGroupId);
        Assert.Null(native.LegacyGroupId);   // created on TeTS, never existed on the legacy platform
    }

    [Fact]
    public async Task PageSize_LandsInQueryString()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Groups.ListAsync(new ListGroupsOptions { PageSize = 500 })) { }
        Assert.Contains("limit=500", handler.Requests[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task TenantOverrideOnOptions_SendsTenantHeader()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Groups.ListAsync(new ListGroupsOptions
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
            client.Groups.ListAsync(new ListGroupsOptions { PageSize = pageSize }));
        Assert.Equal("options", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ListAsync_PageSizeInRangeBoundaries_DoesNotThrow()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null))
               .Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Groups.ListAsync(new ListGroupsOptions { PageSize = 1 })) { }
        await foreach (var _ in client.Groups.ListAsync(new ListGroupsOptions { PageSize = 1000 })) { }
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
            await foreach (var group in client.Groups.ListAsync())
                ids.Add(group.GroupId);
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
            await foreach (var _ in client.Groups.ListAsync()) { }
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
            await foreach (var group in client.Groups.ListAsync())
                ids.Add(group.GroupId);
        });
        Assert.Equal(TetsErrorCode.InternalError, ex.Code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "A" }, ids);
    }
}
