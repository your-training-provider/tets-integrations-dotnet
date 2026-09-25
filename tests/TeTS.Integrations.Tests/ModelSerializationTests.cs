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
    public void CompletionsReport_ProgramRow_IsSelfIdentifying()
    {
        // The contract's courseAndProgram example: one child-course row and one program row for the same learner.
        const string json = """
        {"from":"2026-09-01T00:00:00.000Z","to":"2026-09-30T23:59:59.999Z","count":2,
         "completions":[
           {"userName":"jane.doe","firstName":"Jane","lastName":"Doe","courseName":"CPR Basics","courseId":4521,
            "productId":"6f1c2a1e-6b0e-4a7f-9a3e-0c2c9a1c1a01","productType":"course","legacyProgramId":null,
            "userId":"0f2b7a9c-1d3e-4f5a-8b6c-7d8e9f0a1b2c","finalMark":100,
            "externalId":"3c5f0a2d-8e7b-4d1a-9c6e-2b4f6a8c0e1d","identificationNumber":"3c5f0a2d-8e7b-4d1a-9c6e-2b4f6a8c0e1d",
            "organization":null,"country":null,"completedDate":"2026-09-12T15:04:05.000Z",
            "dateRegistered":"2026-09-01T09:00:00.000Z","email":"jane.doe@example.com","code":"CPR-101",
            "expiresAt":"2028-09-12T15:04:05.000Z"},
           {"userName":"jane.doe","firstName":"Jane","lastName":"Doe","courseName":"DDS Phase 4 10 Hrs","courseId":null,
            "productId":"b9d4e2f0-3a1b-4c5d-8e6f-7a8b9c0d1e2f","productType":"program","legacyProgramId":445,
            "userId":"0f2b7a9c-1d3e-4f5a-8b6c-7d8e9f0a1b2c","finalMark":null,
            "externalId":"3c5f0a2d-8e7b-4d1a-9c6e-2b4f6a8c0e1d","identificationNumber":"3c5f0a2d-8e7b-4d1a-9c6e-2b4f6a8c0e1d",
            "organization":null,"country":null,"completedDate":"2026-09-12T15:04:05.000Z",
            "dateRegistered":"2026-09-01T09:00:00.000Z","email":"jane.doe@example.com","code":"PROGRAM-445",
            "expiresAt":null}],
         "pagination":{"limit":200,"hasMore":false,"nextCursor":null}}
        """;
        var report = JsonSerializer.Deserialize<CompletionsReport>(json, TetsJson.Options)!;
        Assert.Equal(2, report.Completions.Count);

        var course = report.Completions[0];
        Assert.Equal("6f1c2a1e-6b0e-4a7f-9a3e-0c2c9a1c1a01", course.ProductId);
        Assert.Equal("course", course.ProductType);
        Assert.Equal(4521, course.CourseId);
        Assert.Null(course.LegacyProgramId);

        var program = report.Completions[1];
        Assert.Equal("b9d4e2f0-3a1b-4c5d-8e6f-7a8b9c0d1e2f", program.ProductId);
        Assert.Equal("program", program.ProductType);
        Assert.Null(program.CourseId);            // program rows carry no legacy course id
        Assert.Equal(445, program.LegacyProgramId);
        Assert.Equal("DDS Phase 4 10 Hrs", program.CourseName);   // the program title
        Assert.Equal("PROGRAM-445", program.Code);                 // the program SKU
        Assert.Null(program.FinalMark);
        Assert.Null(program.ExpiresAt);
    }

    [Fact]
    public void CompletionRecord_ToleratesPre110ServerWithoutProductFields()
    {
        // A server that predates contract 1.1.0 omits productId/productType/legacyProgramId entirely.
        const string json = """
        {"firstName":"Casey","lastName":"Lee","courseName":"Fire Safety","courseId":42,
         "userId":"5b0d2f1e-0000-0000-0000-000000000001","completedDate":"2026-01-15T10:00:00Z"}
        """;
        var c = JsonSerializer.Deserialize<CompletionRecord>(json, TetsJson.Options)!;
        Assert.Equal(42, c.CourseId);
        Assert.Null(c.ProductId);
        Assert.Null(c.ProductType);
        Assert.Null(c.LegacyProgramId);
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
