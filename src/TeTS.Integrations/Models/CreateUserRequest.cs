using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Request body for creating a platform user linked to your integration.</summary>
public sealed class CreateUserRequest
{
    /// <summary>Your stable staff identifier (e.g. a staff GUID). Required.</summary>
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>Desired platform username. Required.</summary>
    [JsonPropertyName("userName")] public string UserName { get; set; } = "";
    /// <summary>The user's first name. Required.</summary>
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = "";
    /// <summary>The user's last name. Required.</summary>
    [JsonPropertyName("lastName")] public string LastName { get; set; } = "";
    /// <summary>The user's email address. Required.</summary>
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    /// <summary>Optional; a strong random password is generated when omitted.</summary>
    [JsonPropertyName("password")] public string? Password { get; set; }
    /// <summary>The user's organization/company name, when applicable.</summary>
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    /// <summary>Groups to assign; must belong to the resolved organization. Defaults to the connection's configured default group; if none is configured, groupIds is required.</summary>
    [JsonPropertyName("groupIds")] public List<string>? GroupIds { get; set; }
}
