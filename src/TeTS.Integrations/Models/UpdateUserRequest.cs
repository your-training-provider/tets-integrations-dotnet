using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Partial profile update. Identify with <see cref="ExternalId"/> or <see cref="UserId"/>; only set fields are sent.</summary>
public sealed class UpdateUserRequest
{
    /// <summary>Your stable staff identifier for the user to update. Set this or <see cref="UserId"/>.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>Platform user ID of the user to update. Set this or <see cref="ExternalId"/>.</summary>
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    /// <summary>New platform username, if changing it.</summary>
    [JsonPropertyName("userName")] public string? UserName { get; set; }
    /// <summary>New first name, if changing it.</summary>
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    /// <summary>New last name, if changing it.</summary>
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    /// <summary>New email address, if changing it.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }
    /// <summary>New organization/company name, if changing it.</summary>
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    /// <summary>New job title, if changing it.</summary>
    [JsonPropertyName("jobTitle")] public string? JobTitle { get; set; }
}
