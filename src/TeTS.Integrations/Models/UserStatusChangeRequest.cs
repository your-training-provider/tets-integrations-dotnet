using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Wire body for the users/status activate/deactivate endpoint.</summary>
internal sealed class UserStatusChangeRequest
{
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>
    /// Contract-allowed alternate identifier. The SDK identifies users by externalId only
    /// (see <see cref="TeTS.Integrations.Resources.UsersResource"/>), so this is never set by
    /// SDK code; it exists so the model stays a complete mirror of the wire schema.
    /// </summary>
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
}
