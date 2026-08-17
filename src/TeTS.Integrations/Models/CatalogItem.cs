using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One row of the organization's training catalog returned by <c>Catalog.ListAsync</c>.</summary>
public sealed class CatalogItem
{
    /// <summary>Platform-assigned product ID.</summary>
    [JsonPropertyName("productId")] public string ProductId { get; set; } = "";
    /// <summary>The kind of product: <c>course</c>, <c>program</c>, or <c>class</c>.</summary>
    [JsonPropertyName("productType")] public string ProductType { get; set; } = "";
    /// <summary>Display title of the product.</summary>
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    /// <summary>Product SKU, when set.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }
    /// <summary>Names of the categories the product belongs to.</summary>
    [JsonPropertyName("categoryNames")] public IReadOnlyList<string> CategoryNames { get; set; } = Array.Empty<string>();
    /// <summary>Days a completion's certificate stays valid. Null = no expiry.</summary>
    [JsonPropertyName("certValidityDays")] public int? CertValidityDays { get; set; }
    /// <summary>Timestamp the catalog row was last updated.</summary>
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>
    /// Legacy numeric course id — the id the completions report emits as <c>courseId</c> and SSO
    /// accepts as <c>courseId</c>/<c>cid</c>. Null when the product has no legacy course id.
    /// </summary>
    [JsonPropertyName("legacyCourseId")] public int? LegacyCourseId { get; set; }
    /// <summary>
    /// Legacy numeric program id — programs deep-link via the SSO <c>programId</c> parameter.
    /// Null when the product has no legacy program id.
    /// </summary>
    [JsonPropertyName("legacyProgramId")] public int? LegacyProgramId { get; set; }
    /// <summary>
    /// True when this organization superseded the edition via a renewal redirect: the row is kept
    /// for interpreting historical completions and renewals — do not deep-link it for new assignments.
    /// </summary>
    [JsonPropertyName("renewOnly")] public bool RenewOnly { get; set; }
    /// <summary>Child courses for programs, in program order. Null for non-program products.</summary>
    [JsonPropertyName("programCourses")] public IReadOnlyList<CatalogProgramCourse>? ProgramCourses { get; set; }
}

/// <summary>Wire shape of one page of the catalog. Internal; surfaced item-by-item via <c>Catalog.ListAsync</c>.</summary>
internal sealed class CatalogListResponse
{
    [JsonPropertyName("items")] public List<CatalogItem> Items { get; set; } = new();
    [JsonPropertyName("pagination")] public Pagination Pagination { get; set; } = new();
}
