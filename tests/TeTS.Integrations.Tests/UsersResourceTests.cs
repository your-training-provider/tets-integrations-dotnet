using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class UsersResourceTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make()
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = 1 });
        return (client, handler);
    }

    private const string CreatedBody = """
      {"user":{"userId":"5b0d2f1e-0000-0000-0000-000000000001","externalId":"guid-1",
       "userName":"casey.lee","email":"c@example.com","status":"active","groupIds":["g1"]}}
      """;

    [Fact]
    public async Task Create_UnwrapsUser_AndAutoGeneratesIdempotencyKey()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Created, CreatedBody);
        var result = await client.Users.CreateAsync(new CreateUserRequest
        { ExternalId = "guid-1", UserName = "casey.lee", FirstName = "C", LastName = "L", Email = "c@example.com" });
        Assert.Equal("guid-1", result.ExternalId);
        var recorded = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, recorded.Request.Method);
        Assert.EndsWith("/api/integrations/v1/users", recorded.Request.RequestUri!.AbsolutePath);
        var key = Assert.Single(recorded.Request.Headers.GetValues("Idempotency-Key"));
        Assert.StartsWith("tets-sdk-", key);
        Assert.Contains("\"externalId\":\"guid-1\"", recorded.Body);
    }

    [Fact]
    public async Task Create_ReusesSameIdempotencyKeyAcrossRetries()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, """{"error":"x","code":"INTERNAL_ERROR","requestId":"r"}""")
               .Enqueue(HttpStatusCode.Created, CreatedBody);
        await client.Users.CreateAsync(new CreateUserRequest
        { ExternalId = "g", UserName = "u", FirstName = "F", LastName = "L", Email = "e@x.com" });
        Assert.Equal(2, handler.Requests.Count);
        var k1 = Assert.Single(handler.Requests[0].Request.Headers.GetValues("Idempotency-Key"));
        var k2 = Assert.Single(handler.Requests[1].Request.Headers.GetValues("Idempotency-Key"));
        Assert.Equal(k1, k2);
    }

    [Fact]
    public async Task Create_CallerSuppliedKeyWins()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Created, CreatedBody);
        await client.Users.CreateAsync(new CreateUserRequest
        { ExternalId = "g", UserName = "u", FirstName = "F", LastName = "L", Email = "e@x.com" },
            idempotencyKey: "my-key-1");
        Assert.Equal("my-key-1",
            Assert.Single(Assert.Single(handler.Requests).Request.Headers.GetValues("Idempotency-Key")));
    }

    [Fact]
    public async Task GetByExternalId_BuildsQuery_AndUnwraps()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """
          {"user":{"userId":"u1","externalId":"g 1","firstName":"A","lastName":"B","status":"active"}}
          """);
        var user = await client.Users.GetByExternalIdAsync("g 1");
        Assert.Equal("g 1", user.ExternalId);
        Assert.Equal("/api/integrations/v1/users", handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal("?externalId=g%201", handler.Requests[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task Update_SendsPatch()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """
          {"user":{"userId":"u1","externalId":"g","firstName":"A","lastName":"B","status":"active","jobTitle":"RN"}}
          """);
        var user = await client.Users.UpdateAsync(new UpdateUserRequest { ExternalId = "g", JobTitle = "RN" });
        Assert.Equal("RN", user.JobTitle);
        Assert.Equal("PATCH", handler.Requests[0].Request.Method.Method);
        Assert.DoesNotContain("firstName", handler.Requests[0].Body);
    }

    [Fact]
    public async Task CheckExists_UsesUserNameQuery()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK,
            """{"exists":false,"linkedToIntegration":false,"userName":"new.name"}""");
        var exists = await client.Users.CheckExistsAsync("new.name");
        Assert.False(exists.Exists);
        Assert.Equal("?userName=new.name", handler.Requests[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task DeactivateAndActivate_SendStatusBody()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """{"user":{"userId":"u1","externalId":"g","status":"inactive"}}""")
               .Enqueue(HttpStatusCode.OK, """{"user":{"userId":"u1","externalId":"g","status":"active"}}""");
        var off = await client.Users.DeactivateAsync("g");
        Assert.Equal("inactive", off.Status);
        Assert.EndsWith("/users/status", handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"status\":\"inactive\"", handler.Requests[0].Body);
        Assert.Contains("\"externalId\":\"g\"", handler.Requests[0].Body);
        var on = await client.Users.ActivateAsync("g");
        Assert.Equal("active", on.Status);
        Assert.Contains("\"status\":\"active\"", handler.Requests[1].Body);
    }

    [Fact]
    public async Task GetByExternalId_EmptyEnvelope_ThrowsTetsApiException_NotNullOrNRE()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, "{}");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.Users.GetByExternalIdAsync("g"));
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
    }

    [Fact]
    public async Task Create_NullUserInEnvelope_ThrowsTetsApiException_NotNullOrNRE()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Created, """{"user":null}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.Users.CreateAsync(new CreateUserRequest
        { ExternalId = "g", UserName = "u", FirstName = "F", LastName = "L", Email = "e@x.com" }));
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
    }

    [Fact]
    public async Task GetByExternalId_NotFound_SurfacesTetsApiException_WithCodeAndRequestId()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.NotFound,
            """{"error":"No such user.","code":"INTEGRATION_USER_NOT_FOUND","requestId":"req_404"}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.Users.GetByExternalIdAsync("missing"));
        Assert.Equal(TetsErrorCode.IntegrationUserNotFound, ex.Code);
        Assert.Equal("req_404", ex.RequestId);
    }

    [Fact]
    public async Task CreateAsync_NullRequest_ThrowsArgumentNullException_BeforeAnyRequest()
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => client.Users.CreateAsync(null!));
        Assert.Equal("request", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateAsync_NullRequest_ThrowsArgumentNullException_BeforeAnyRequest()
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => client.Users.UpdateAsync(null!));
        Assert.Equal("request", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateAsync_NoIdentifier_ThrowsArgumentException_BeforeAnyRequest()
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Users.UpdateAsync(new UpdateUserRequest { JobTitle = "RN" }));
        Assert.Equal("request", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByExternalIdAsync_BlankExternalId_ThrowsArgumentException_BeforeAnyRequest(string? externalId)
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.Users.GetByExternalIdAsync(externalId!));
        Assert.Equal("externalId", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ActivateAsync_BlankExternalId_ThrowsArgumentException_BeforeAnyRequest(string? externalId)
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.Users.ActivateAsync(externalId!));
        Assert.Equal("externalId", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeactivateAsync_BlankExternalId_ThrowsArgumentException_BeforeAnyRequest(string? externalId)
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.Users.DeactivateAsync(externalId!));
        Assert.Equal("externalId", ex.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckExistsAsync_BlankUserName_ThrowsArgumentException_BeforeAnyRequest(string? userName)
    {
        var (client, handler) = Make();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.Users.CheckExistsAsync(userName!));
        Assert.Equal("userName", ex.ParamName);
        Assert.Empty(handler.Requests);
    }
}
