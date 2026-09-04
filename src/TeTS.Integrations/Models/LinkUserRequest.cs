using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>
/// Request body for <see cref="TeTS.Integrations.Resources.UsersResource.LinkAsync"/>: attaches your
/// <see cref="ExternalId"/> to a platform user that already exists but is not yet linked to your
/// integration (for example a learner a manager created in the TeTS UI, or one migrated without an
/// ID on file). Set exactly one of <see cref="UserId"/> or <see cref="UserName"/>.
/// </summary>
public sealed class LinkUserRequest
{
    /// <summary>Your stable staff identifier for the user. Required.</summary>
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>Platform user ID, as returned by <c>Users.ListAsync</c> rows with a null <c>ExternalId</c>.</summary>
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    /// <summary>Platform username, as checked by <c>Users.CheckExistsAsync</c>.</summary>
    [JsonPropertyName("userName")] public string? UserName { get; set; }
}
