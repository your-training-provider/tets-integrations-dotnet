using System.Text.Json;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class ModelSerializationTests
{
    [Fact]
    public void CreateUserRequest_SerializesWithWireNames_AndOmitsNulls()
    {
        var json = JsonSerializer.Serialize(new CreateUserRequest
        {
            ExternalId = "guid-1", UserName = "casey.lee",
            FirstName = "Casey", LastName = "Lee", Email = "c@example.com",
        }, TetsJson.Options);
        Assert.Contains("\"externalId\":\"guid-1\"", json);
        Assert.Contains("\"userName\":\"casey.lee\"", json);
        Assert.DoesNotContain("password", json);   // null optional omitted
        Assert.DoesNotContain("groupIds", json);
    }

    [Fact]
    public void CompletionsReport_Deserializes()
    {
        const string json = """
        {"from":"2026-01-01T00:00:00Z","to":"2026-01-31T23:59:59Z","count":1,
         "completions":[{"userName":"casey.lee","firstName":"Casey","lastName":"Lee",
           "courseName":"Fire Safety","courseId":42,"userId":"5b0d2f1e-0000-0000-0000-000000000001",
           "finalMark":95.5,"externalId":"guid-1","completedDate":"2026-01-15T10:00:00Z",
           "code":"FS-101","expiresAt":null}],
         "pagination":{"limit":200,"hasMore":true,"nextCursor":"abc"}}
        """;
        var report = JsonSerializer.Deserialize<CompletionsReport>(json, TetsJson.Options)!;
        Assert.Equal(1, report.Count);
        var c = Assert.Single(report.Completions);
        Assert.Equal("Fire Safety", c.CourseName);
        Assert.Equal(42, c.CourseId);
        Assert.Equal(95.5, c.FinalMark);
        Assert.Null(c.ExpiresAt);
        Assert.True(report.Pagination.HasMore);
        Assert.Equal("abc", report.Pagination.NextCursor);
    }

    [Fact]
    public void User_ToleratesUnknownFields()
    {
        const string json = """
        {"userId":"5b0d2f1e-0000-0000-0000-000000000001","externalId":"g",
            "firstName":"A","lastName":"B","status":"active","brandNewServerField":123}
        """;
        var user = JsonSerializer.Deserialize<User>(json, TetsJson.Options)!;
        Assert.Equal("active", user.Status);
    }

    [Fact]
    public void UpdateUserRequest_OmitsUnsetFields()
    {
        var json = JsonSerializer.Serialize(new UpdateUserRequest { ExternalId = "g", JobTitle = "RN" }, TetsJson.Options);
        Assert.Contains("jobTitle", json);
        Assert.DoesNotContain("firstName", json);  // PATCH partial semantics depend on omission
    }
}
