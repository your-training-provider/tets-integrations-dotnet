using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>A platform user linked to your integration.</summary>
public sealed class User
{
    /// <summary>Platform-assigned user ID.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>Your stable staff identifier for this user.</summary>
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>The user's platform username, when set.</summary>
    [JsonPropertyName("userName")] public string? UserName { get; set; }
    /// <summary>The user's first name.</summary>
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = "";
    /// <summary>The user's last name.</summary>
    [JsonPropertyName("lastName")] public string LastName { get; set; } = "";
    /// <summary>The user's email address, when set.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }
    /// <summary>The user's account status, e.g. <c>active</c> or <c>inactive</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    /// <summary>The user's organization/company name, when set.</summary>
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    /// <summary>The user's job title, when set.</summary>
    [JsonPropertyName("jobTitle")] public string? JobTitle { get; set; }
}
