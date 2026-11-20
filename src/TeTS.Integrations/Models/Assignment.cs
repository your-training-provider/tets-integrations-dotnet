using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>
/// One learner's assignment of one title, as returned by <c>Assignments.AssignAsync</c> and the
/// <c>Assignments.GetBy…Async</c> reads.
/// </summary>
public sealed class Assignment
{
    /// <summary>Platform assignment ID.</summary>
    [JsonPropertyName("assignmentId")] public string AssignmentId { get; set; } = "";
    /// <summary>Platform user ID of the learner.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>
    /// Your stable staff identifier for the learner: the one you sent, or the one the server resolved
    /// for a <c>userId</c>.
    /// </summary>
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>Platform product ID of the assigned title; joins to <see cref="CatalogItem.ProductId"/>.</summary>
    [JsonPropertyName("productId")] public string ProductId { get; set; } = "";
    /// <summary>The kind of title: <c>course</c>, <c>program</c>, or <c>class</c> (same values as <see cref="CatalogItem.ProductType"/>).</summary>
    [JsonPropertyName("productType")] public string ProductType { get; set; } = "";
    /// <summary>Display title of the assigned product.</summary>
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    /// <summary>
    /// Where the learner stands, exactly as the app shows it to the learner and their manager:
    /// <c>assigned</c> (the app's "not started"), <c>in_progress</c>, <c>completed</c>, or
    /// <c>expired</c> (a completion whose certificate has lapsed, so the title is due again). Treat an
    /// unrecognized value as a status this SDK version does not know yet.
    /// </summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    /// <summary>When the assignment was made.</summary>
    [JsonPropertyName("assignedAt")] public DateTimeOffset AssignedAt { get; set; }
    /// <summary>
    /// When the title was last completed in this assignment's cycle; null until then. Can also be null
    /// on a <c>completed</c> or <c>expired</c> row whose status rests on a compliance requirement with
    /// no completion record of its own (most likely migrated history).
    /// </summary>
    [JsonPropertyName("completedAt")] public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>
    /// Certificate expiry of that completion; null until completed, or when the title does not expire.
    /// </summary>
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>Wire shape of <c>GET /assignments</c>. Internal; surfaced as a list by <c>Assignments.GetBy…Async</c>.</summary>
internal sealed class AssignmentListResponse
{
    [JsonPropertyName("assignments")] public List<Assignment> Assignments { get; set; } = new();
}
