using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>The user created by <c>Users.CreateAsync</c>.</summary>
public sealed class CreateUserResult
{
    /// <summary>Platform-assigned user ID.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>Your stable staff identifier, as submitted on the create request.</summary>
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>The user's platform username.</summary>
    [JsonPropertyName("userName")] public string UserName { get; set; } = "";
    /// <summary>The user's email address.</summary>
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    /// <summary>The user's account status, e.g. <c>active</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    /// <summary>Groups the user was assigned to.</summary>
    [JsonPropertyName("groupIds")] public List<string> GroupIds { get; set; } = new();
}
