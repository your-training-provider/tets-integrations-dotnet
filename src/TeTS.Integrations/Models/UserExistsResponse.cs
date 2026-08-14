using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Username availability and integration linkage for a candidate username.</summary>
public sealed class UserExistsResponse
{
    /// <summary>True when the username is already taken by any user in the resolved organization.</summary>
    [JsonPropertyName("exists")] public bool Exists { get; set; }
    /// <summary>True only when the user is linked to YOUR integration in the resolved organization.</summary>
    [JsonPropertyName("linkedToIntegration")] public bool LinkedToIntegration { get; set; }
    /// <summary>The username that was checked, echoed back from the request.</summary>
    [JsonPropertyName("userName")] public string UserName { get; set; } = "";
    /// <summary>Platform user ID of the matching user, when <see cref="Exists"/> is true.</summary>
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    /// <summary>Your stable staff identifier for the matching user, when linked to your integration.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
}
