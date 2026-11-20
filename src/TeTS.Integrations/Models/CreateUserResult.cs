using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>The user created by <c>Users.CreateAsync</c>.</summary>
public sealed class CreateUserResult
{
    /// <summary>Platform-assigned user ID.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>Your stable staff identifier, as submitted on the create request.</summary>
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = "";
    /// <summary>
    /// The account's own platform username. Differs from the <c>UserName</c> you sent when the create
    /// linked an existing account instead of creating one (see <see cref="IntegrationUserName"/>).
    /// </summary>
    [JsonPropertyName("userName")] public string UserName { get; set; } = "";
    /// <summary>
    /// Set only when the create linked an existing account in your organization instead of creating a
    /// new one: the <c>UserName</c> you sent. Keep using it; SSO launches and
    /// <c>Users.CheckExistsAsync</c> resolve it to this account. Null on a plain create, and on servers
    /// that predate contract 1.2.0.
    /// </summary>
    [JsonPropertyName("integrationUserName")] public string? IntegrationUserName { get; set; }
    /// <summary>The user's email address.</summary>
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    /// <summary>The user's account status, e.g. <c>active</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    /// <summary>Groups the user was assigned to.</summary>
    [JsonPropertyName("groupIds")] public List<string> GroupIds { get; set; } = new();
}
