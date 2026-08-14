using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Result of an activate/deactivate call.</summary>
public sealed class UserStatusResult
{
    /// <summary>Platform-assigned user ID.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>Your stable staff identifier for this user, when linked to your integration.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>The user's resulting account status, e.g. <c>active</c> or <c>inactive</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "";
}
