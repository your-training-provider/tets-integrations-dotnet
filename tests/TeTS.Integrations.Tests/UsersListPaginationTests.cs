using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class UsersListPaginationTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k" });
        return (client, handler);
    }

    private static string Page(string first, bool hasMore, string? next) => $$$"""
      {"users":[{"userId":"{{{first}}}","externalId":"ext-{{{first}}}","userName":"user.{{{first}}}",
        "firstName":"F","lastName":"L","email":"{{{first}}}@example.com","status":"active","groupIds":["g1"]}],
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
        await foreach (var user in client.Users.ListAsync())
            ids.Add(user.UserId);
        Assert.Equal(new[] { "A", "B", "C" }, ids);
        Assert.Equal(3, handler.Requests.Count);
        Assert.DoesNotContain("cursor", handler.Requests[0].Request.RequestUri!.Query);
        Assert.Contains("cursor=c2", handler.Requests[1].Request.RequestUri!.Query);
        Assert.Contains("cursor=c3", handler.Requests[2].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task SinglePage_MakesExactlyOneRequest_ToTheListPath()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        var users = new List<UserListItem>();
        await foreach (var user in client.Users.ListAsync())
            users.Add(user);
        var recorded = Assert.Single(handler.Requests);
        Assert.EndsWith("/api/integrations/v1/users/list", recorded.Request.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, recorded.Request.Method);
        var u = Assert.Single(users);
        Assert.Equal("ext-A", u.ExternalId);
        Assert.Equal("user.A", u.UserName);
        Assert.Equal(new[] { "g1" }, u.GroupIds);
    }

    [Fact]
    public async Task GroupIdAndPageSize_LandInQueryString()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Users.ListAsync(new ListUsersOptions
        { GroupId = "5b0d2f1e-0000-0000-0000-00000000000a", PageSize = 500 })) { }
        var q = handler.Requests[0].Request.RequestUri!.Query;
        Assert.Contains("limit=500", q);
        Assert.Contains("groupId=5b0d2f1e-0000-0000-0000-00000000000a", q);
    }

    [Fact]
    public async Task TenantOverrideOnOptions_SendsTenantHeader()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Users.ListAsync(new ListUsersOptions
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
            client.Users.ListAsync(new ListUsersOptions { PageSize = pageSize }));
        Assert.Equal("options", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ListAsync_PageSizeInRangeBoundaries_DoesNotThrow()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", false, null))
               .Enqueue(HttpStatusCode.OK, Page("A", false, null));
        await foreach (var _ in client.Users.ListAsync(new ListUsersOptions { PageSize = 1 })) { }
        await foreach (var _ in client.Users.ListAsync(new ListUsersOptions { PageSize = 1000 })) { }
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("limit=1", handler.Requests[0].Request.RequestUri!.Query);
        Assert.Contains("limit=1000", handler.Requests[1].Request.RequestUri!.Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ListAsync_BlankGroupId_ThrowsArgumentException_BeforeAnyRequest(string groupId)
    {
        var (client, handler) = Make();
        var ex = Assert.Throws<ArgumentException>(() =>
            client.Users.ListAsync(new ListUsersOptions { GroupId = groupId }));
        Assert.Equal("options", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task NullExternalId_AndMissingOptionalFields_DeserializeToNulls()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """
          {"users":[{"userId":"u1","externalId":null,"firstName":"Migrated","lastName":"Account",
            "status":"active","groupIds":[]}],
           "pagination":{"limit":200,"hasMore":false,"nextCursor":null}}
          """);
        var users = new List<UserListItem>();
        await foreach (var user in client.Users.ListAsync())
            users.Add(user);
        var u = Assert.Single(users);
        Assert.Null(u.ExternalId);   // not yet linked to the integration
        Assert.Null(u.UserName);     // key absent entirely
        Assert.Null(u.Email);        // key absent entirely
        Assert.Equal("Migrated", u.FirstName);
        Assert.Empty(u.GroupIds);
    }

    [Fact]
    public async Task Enumerable_ServerReturnsSameCursorTwice_ThrowsAfterExactlyTwoRequests_PreservingYieldedUsers()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, Page("A", true, "c2"))
               .Enqueue(HttpStatusCode.OK, Page("B", true, "c2"));
        var ids = new List<string>();
        var ex = await Assert.ThrowsAsync<TetsApiException>(async () =>
        {
            await foreach (var user in client.Users.ListAsync())
                ids.Add(user.UserId);
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
            await foreach (var _ in client.Users.ListAsync()) { }
        });
        Assert.Equal(TetsErrorCode.IntegrationConnectionForbidden, ex.Code);
        Assert.Equal("req_403", ex.RequestId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Enumerable_MidPaginationFailure_PreservesFirstPageUsers_ThenThrowsOnSecondPull()
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
            await foreach (var user in client.Users.ListAsync())
                ids.Add(user.UserId);
        });
        Assert.Equal(TetsErrorCode.InternalError, ex.Code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new[] { "A" }, ids);
    }
}
