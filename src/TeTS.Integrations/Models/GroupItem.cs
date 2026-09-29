using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One group in the organization's group directory returned by <c>Groups.ListAsync</c>.</summary>
public sealed class GroupItem
{
    /// <summary>
    /// Platform group id: the value <c>GroupIds</c> (user create/update) and
    /// <see cref="ListUsersOptions.GroupId"/> take.
    /// </summary>
    [JsonPropertyName("groupId")] public string GroupId { get; set; } = "";
    /// <summary>Display name of the group.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    /// <summary>Parent group id. Null for the organization root.</summary>
    [JsonPropertyName("parentGroupId")] public string? ParentGroupId { get; set; }
    /// <summary>
    /// True for the organization root group, whose <see cref="GroupId"/> is the organization tenant id.
    /// </summary>
    [JsonPropertyName("isOrganizationRoot")] public bool IsOrganizationRoot { get; set; }
    /// <summary>
    /// False for the organization root, which is record-only: sending its id in <c>GroupIds</c> is
    /// rejected. Place users in groups where this is true.
    /// </summary>
    [JsonPropertyName("acceptsMembers")] public bool AcceptsMembers { get; set; }
    /// <summary>
    /// The group's id on the legacy platform, for migrated groups. It is the same in every
    /// environment, so resolve the group ids you already hold through it. Null for groups created
    /// on TeTS.
    /// </summary>
    [JsonPropertyName("legacyGroupId")] public string? LegacyGroupId { get; set; }
    /// <summary>Timestamp the group was created.</summary>
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Timestamp the group was last updated.</summary>
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Wire shape of one page of the group directory. Internal; surfaced item-by-item via <c>Groups.ListAsync</c>.</summary>
internal sealed class GroupListResponse
{
    [JsonPropertyName("items")] public List<GroupItem> Items { get; set; } = new();
    [JsonPropertyName("pagination")] public Pagination Pagination { get; set; } = new();
}
