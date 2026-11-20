using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>
/// One completed training for a user linked to your integration. Since server contract 1.1.0 every
/// row is self-identifying: <see cref="ProductId"/> and <see cref="ProductType"/> join to
/// <c>Catalog.ListAsync</c> rows for any product type. A finished program produces one row per child
/// course (each with its own <see cref="CourseId"/>) plus one program row with
/// <see cref="ProductType"/> <c>program</c>, <see cref="CourseId"/> null, and
/// <see cref="LegacyProgramId"/> set.
/// </summary>
public sealed class CompletionRecord
{
    /// <summary>
    /// Platform transcript record id: one per completion, the same on every pull of a range that
    /// includes it, and unchanged when the id the completion is reported under changes (a migrated or
    /// aliased row). Key stored completions on this rather than on (<see cref="UserId"/>,
    /// <see cref="CourseId"/>, <see cref="CompletedDate"/>), so re-pulling a range never creates a
    /// second record. Empty only when the server predates contract 1.2.0.
    /// </summary>
    [JsonPropertyName("recordId")] public string RecordId { get; set; } = "";
    /// <summary>Platform username of the learner, when available.</summary>
    [JsonPropertyName("userName")] public string? UserName { get; set; }
    /// <summary>The learner's first name.</summary>
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = "";
    /// <summary>The learner's last name.</summary>
    [JsonPropertyName("lastName")] public string LastName { get; set; } = "";
    /// <summary>Title of the completed product; the program title on a program row.</summary>
    [JsonPropertyName("courseName")] public string CourseName { get; set; } = "";
    /// <summary>
    /// Legacy numeric course id when available. Null on program rows (use <see cref="LegacyProgramId"/>).
    /// For a completion migrated from Topyx this is the Topyx course id the completion was recorded
    /// under (the id SSO accepts), even when the platform filed it on a content-identical twin product.
    /// </summary>
    [JsonPropertyName("courseId")] public int? CourseId { get; set; }
    /// <summary>
    /// Platform product id of the completed training. For a completion earned on the platform it joins
    /// to <c>CatalogItem.ProductId</c>; for a completion migrated from Topyx it is the product the
    /// recorded Topyx id maps to, which the organization's catalog may not list, so key migrated rows
    /// on <see cref="CourseId"/> / <see cref="LegacyProgramId"/>. Null only when the server predates
    /// contract 1.1.0.
    /// </summary>
    [JsonPropertyName("productId")] public string? ProductId { get; set; }
    /// <summary>
    /// The kind of product completed: <c>course</c>, <c>program</c>, or <c>class</c> (same values as
    /// <c>CatalogItem.ProductType</c>). Null only when the server predates contract 1.1.0.
    /// </summary>
    [JsonPropertyName("productType")] public string? ProductType { get; set; }
    /// <summary>
    /// Legacy numeric program id on program rows, the same id <c>CatalogItem.LegacyProgramId</c> carries
    /// and SSO <c>programId</c> accepts. Null for courses and classes.
    /// </summary>
    [JsonPropertyName("legacyProgramId")] public int? LegacyProgramId { get; set; }
    /// <summary>Platform user ID of the learner.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>Final numeric score/mark, when the course records one.</summary>
    [JsonPropertyName("finalMark")] public double? FinalMark { get; set; }
    /// <summary>Your stable staff identifier for the learner, when linked to your integration.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>Alternate identification number for the learner, when configured.</summary>
    [JsonPropertyName("identificationNumber")] public string? IdentificationNumber { get; set; }
    /// <summary>The learner's organization/company name, when recorded.</summary>
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    /// <summary>The learner's country, when recorded.</summary>
    [JsonPropertyName("country")] public string? Country { get; set; }
    /// <summary>Timestamp the completion was recorded.</summary>
    [JsonPropertyName("completedDate")] public DateTimeOffset CompletedDate { get; set; }
    /// <summary>Timestamp the learner was registered for the course, when available.</summary>
    [JsonPropertyName("dateRegistered")] public DateTimeOffset? DateRegistered { get; set; }
    /// <summary>The learner's email address, when recorded.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }
    /// <summary>Product SKU.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }
    /// <summary>When the completion/certification expires, for courses with a renewal cycle.</summary>
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; set; }
}
