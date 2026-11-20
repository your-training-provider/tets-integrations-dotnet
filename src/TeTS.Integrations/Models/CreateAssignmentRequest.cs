using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>
/// Request body for <see cref="TeTS.Integrations.Resources.AssignmentsResource.AssignAsync"/>: assigns
/// one course or program to one learner. Set <see cref="ProductId"/> and exactly one of
/// <see cref="ExternalId"/> or <see cref="UserId"/>.
/// </summary>
public sealed class CreateAssignmentRequest
{
    /// <summary>
    /// Your stable staff identifier for the learner (the <c>externalId</c> they were created or linked
    /// under). Set this or <see cref="UserId"/>, not both.
    /// </summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>Platform user ID (from the user calls). Alternative to <see cref="ExternalId"/>.</summary>
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    /// <summary>
    /// The title to assign: a <see cref="CatalogItem.ProductId"/> from <c>Catalog.ListAsync</c>, for a
    /// course or a program your organization carries. Required. Legacy ids are not accepted here.
    /// </summary>
    [JsonPropertyName("productId")] public string ProductId { get; set; } = "";
}
