using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One user in the organization roster returned by <c>Users.ListAsync</c>.</summary>
public sealed class UserListItem
{
    /// <summary>Platform-assigned user ID.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>
    /// Your stable staff identifier for this user. Null when the user is not yet linked to your
    /// integration — for example an account migrated from the legacy platform that has not yet been
    /// linked (linking happens on the user's first SSO launch with <c>identification</c>, or via a
    /// TeTS bulk link).
    /// </summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
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
    /// <summary>Groups the user belongs to within the resolved organization.</summary>
    [JsonPropertyName("groupIds")] public IReadOnlyList<string> GroupIds { get; set; } = Array.Empty<string>();
}

/// <summary>Wire shape of one page of the user list. Internal; surfaced item-by-item via <c>Users.ListAsync</c>.</summary>
internal sealed class UserListResponse
{
    [JsonPropertyName("users")] public List<UserListItem> Users { get; set; } = new();
    [JsonPropertyName("pagination")] public Pagination Pagination { get; set; } = new();
}
