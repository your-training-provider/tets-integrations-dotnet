using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class AssignmentsResourceTests
{
    private static (TetsIntegrationsClient, TestHttpHandler) Make(int maxRetries = 1)
    {
        var handler = new TestHttpHandler();
        var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = maxRetries });
        return (client, handler);
    }

    // The contract's 201 example.
    private const string CreatedBody = """
      {"created":true,"assignment":{"assignmentId":"0b6f3c1e-9a52-4d0e-8f4b-7c1d2e3a4b5c",
       "userId":"3e8d9a40-1c2b-4f6d-a7e5-9b0c1d2e3f4a","externalId":"E-1042",
       "productId":"course-6f1c2a7e-3b0d-4c55-9a61-2d6c8f0e41aa","productType":"course",
       "title":"Infection Control","status":"assigned","assignedAt":"2026-11-19T18:30:00.000Z",
       "completedAt":null,"expiresAt":null}}
      """;

    // The contract's GET example.
    private const string ListBody = """
      {"assignments":[{"assignmentId":"0b6f3c1e-9a52-4d0e-8f4b-7c1d2e3a4b5c",
       "userId":"3e8d9a40-1c2b-4f6d-a7e5-9b0c1d2e3f4a","externalId":"E-1042",
       "productId":"course-6f1c2a7e-3b0d-4c55-9a61-2d6c8f0e41aa","productType":"course",
       "title":"Infection Control","status":"completed","assignedAt":"2026-11-19T18:30:00.000Z",
       "completedAt":"2026-11-20T14:05:00.000Z","expiresAt":"2027-11-20T14:05:00.000Z"}]}
      """;

    [Fact]
    public async Task Assign_ByExternalId_PostsBody_AndReturnsCreatedAssignment()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Created, CreatedBody);

        var result = await client.Assignments.AssignAsync(new CreateAssignmentRequest
        { ExternalId = "E-1042", ProductId = "course-6f1c2a7e-3b0d-4c55-9a61-2d6c8f0e41aa" });

        Assert.True(result.Created);
        Assert.Equal("0b6f3c1e-9a52-4d0e-8f4b-7c1d2e3a4b5c", result.Assignment.AssignmentId);
        Assert.Equal("assigned", result.Assignment.Status);
        Assert.Equal("course", result.Assignment.ProductType);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 18, 30, 0, TimeSpan.Zero), result.Assignment.AssignedAt);
        Assert.Null(result.Assignment.CompletedAt);
        Assert.Null(result.Assignment.ExpiresAt);

        var recorded = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, recorded.Request.Method);
        Assert.Equal("/api/integrations/v1/assignments", recorded.Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"externalId\":\"E-1042\"", recorded.Body);
        Assert.Contains("\"productId\":\"course-6f1c2a7e-3b0d-4c55-9a61-2d6c8f0e41aa\"", recorded.Body);
        Assert.DoesNotContain("userId", recorded.Body);
        // Repeats are answered by the server (created:false); the SDK does not add an Idempotency-Key.
        Assert.False(recorded.Request.Headers.Contains("Idempotency-Key"));
    }

    [Fact]
    public async Task Assign_ByUserId_SendsUserIdOnly()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Created, CreatedBody);
        await client.Assignments.AssignAsync(new CreateAssignmentRequest { UserId = "u-1", ProductId = "p-1" });
        var body = Assert.Single(handler.Requests).Body;
        Assert.Contains("\"userId\":\"u-1\"", body);
        Assert.DoesNotContain("externalId", body);
    }

    [Fact]
    public async Task Assign_Repeat_Returns200WithCreatedFalse()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, CreatedBody.Replace("\"created\":true", "\"created\":false")
            .Replace("\"status\":\"assigned\"", "\"status\":\"in_progress\""));
        var result = await client.Assignments.AssignAsync(new CreateAssignmentRequest { ExternalId = "E-1042", ProductId = "p" });
        Assert.False(result.Created);
        Assert.Equal("in_progress", result.Assignment.Status);
    }

    [Fact]
    public async Task Assign_RetriesTransientFailure_AndSendsTheSameBody()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, """{"error":"x","code":"INTERNAL_ERROR","requestId":"r"}""")
               .Enqueue(HttpStatusCode.OK, CreatedBody.Replace("\"created\":true", "\"created\":false"));
        var result = await client.Assignments.AssignAsync(new CreateAssignmentRequest { ExternalId = "E-1042", ProductId = "p" });
        Assert.False(result.Created);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "SEATS_EXHAUSTED", TetsErrorCode.SeatsExhausted)]
    [InlineData(HttpStatusCode.NotFound, "PRODUCT_NOT_AVAILABLE", TetsErrorCode.ProductNotAvailable)]
    [InlineData(HttpStatusCode.Forbidden, "ORG_BASIC_SUBSCRIPTION_LAPSED", TetsErrorCode.OrgBasicSubscriptionLapsed)]
    [InlineData(HttpStatusCode.Forbidden, "INSUFFICIENT_SCOPE", TetsErrorCode.InsufficientScope)]
    [InlineData(HttpStatusCode.NotFound, "INTEGRATION_USER_NOT_FOUND", TetsErrorCode.IntegrationUserNotFound)]
    public async Task Assign_Refusal_SurfacesTypedCode_AndIsNotRetried(HttpStatusCode status, string wire, TetsErrorCode expected)
    {
        var (client, handler) = Make(maxRetries: 3);
        handler.Enqueue(status, $$"""{"error":"refused","code":"{{wire}}","requestId":"req_1"}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() =>
            client.Assignments.AssignAsync(new CreateAssignmentRequest { ExternalId = "E-1042", ProductId = "p" }));
        Assert.Equal(expected, ex.Code);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("req_1", ex.RequestId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Assign_MissingAssignmentObject_ThrowsTetsApiException_NotNullOrNRE()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.Created, """{"created":true}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() =>
            client.Assignments.AssignAsync(new CreateAssignmentRequest { ExternalId = "E-1042", ProductId = "p" }));
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
    }

    [Fact]
    public async Task Assign_NullRequest_ThrowsArgumentNullException_BeforeAnyRequest()
    {
        var (client, handler) = Make();
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.Assignments.AssignAsync(null!));
        Assert.Empty(handler.Requests);
    }

    public static TheoryData<string?, string?, string?> InvalidAssignRequests => new()
    {
        { "E-1042", null, null },        // no productId
        { "E-1042", null, "  " },        // blank productId
        { null, null, "p" },             // no learner
        { " ", "", "p" },                // blank learner identifiers
        { "E-1042", "u-1", "p" },        // both identifiers
    };

    [Theory]
    [MemberData(nameof(InvalidAssignRequests))]
    public async Task Assign_InvalidRequest_ThrowsArgumentException_BeforeAnyRequest(string? externalId, string? userId, string? productId)
    {
        var (client, handler) = Make();
        await Assert.ThrowsAsync<ArgumentException>(() => client.Assignments.AssignAsync(new CreateAssignmentRequest
        { ExternalId = externalId, UserId = userId, ProductId = productId! }));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetByExternalId_BuildsQuery_AndReturnsAssignments()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, ListBody);

        var list = await client.Assignments.GetByExternalIdAsync("E 1042");

        var a = Assert.Single(list);
        Assert.Equal("completed", a.Status);
        Assert.Equal("Infection Control", a.Title);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 14, 5, 0, TimeSpan.Zero), a.CompletedAt);
        Assert.Equal(new DateTimeOffset(2027, 11, 20, 14, 5, 0, TimeSpan.Zero), a.ExpiresAt);
        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/integrations/v1/assignments", request.RequestUri!.AbsolutePath);
        Assert.Equal("?externalId=E%201042", request.RequestUri.Query);
    }

    [Fact]
    public async Task GetByUserId_WithProductFilter_BuildsQuery()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """{"assignments":[]}""");
        var list = await client.Assignments.GetByUserIdAsync("u-1", productId: "p-1", organizationTenantId: "t-1");
        Assert.Empty(list);
        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal("?userId=u-1&productId=p-1", request.RequestUri!.Query);
        Assert.Equal("t-1", Assert.Single(request.Headers.GetValues("X-Integration-Tenant-Id")));
    }

    [Fact]
    public async Task Get_MissingAssignmentsArray_ThrowsTetsApiException()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, """{"assignments":null}""");
        var ex = await Assert.ThrowsAsync<TetsApiException>(() => client.Assignments.GetByExternalIdAsync("E-1042"));
        Assert.Equal(TetsErrorCode.Unknown, ex.Code);
    }

    [Fact]
    public async Task Get_UnknownStatus_IsPassedThrough()
    {
        var (client, handler) = Make();
        handler.Enqueue(HttpStatusCode.OK, ListBody.Replace("\"status\":\"completed\"", "\"status\":\"some_future_status\""));
        var a = Assert.Single(await client.Assignments.GetByExternalIdAsync("E-1042"));
        Assert.Equal("some_future_status", a.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_BlankIdentifier_ThrowsArgumentException_Synchronously(string? id)
    {
        var (client, handler) = Make();
        Assert.Throws<ArgumentException>(() => { _ = client.Assignments.GetByExternalIdAsync(id!); });
        Assert.Throws<ArgumentException>(() => { _ = client.Assignments.GetByUserIdAsync(id!); });
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Get_BlankProductFilter_ThrowsArgumentException_Synchronously()
    {
        var (client, handler) = Make();
        Assert.Throws<ArgumentException>(() => { _ = client.Assignments.GetByExternalIdAsync("E-1042", productId: " "); });
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Calls_PassTheCallersCancellationTokenToTheTransport()
    {
        // Like a real socket handler, this one throws once the token it was handed is cancelled. A
        // caller-cancelled request is not retried, so one attempt per call.
        var handler = new CancellationObservingHandler();
        using var client = new TetsIntegrationsClient(new HttpClient(handler),
            new TetsOptions { BaseUrl = "https://api.example.com", ApiKey = "k", MaxRetries = 3 });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Assignments.AssignAsync(new CreateAssignmentRequest { ExternalId = "E-1042", ProductId = "p" }, cancellationToken: cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Assignments.GetByExternalIdAsync("E-1042", cancellationToken: cts.Token));
        Assert.Equal(2, handler.Calls);
    }

    private sealed class CancellationObservingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The test expects a cancelled token.");
        }
    }
}
