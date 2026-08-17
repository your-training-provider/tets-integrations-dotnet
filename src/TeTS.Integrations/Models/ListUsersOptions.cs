namespace TeTS.Integrations.Models;

/// <summary>Options for <c>Users.ListAsync</c>. Every member is optional.</summary>
public sealed class ListUsersOptions
{
    /// <summary>Restrict results to members of this group; must belong to the resolved organization.</summary>
    public string? GroupId { get; set; }
    /// <summary>Page size per underlying request, 1..1000. Server default (200) applies when omitted.</summary>
    public int? PageSize { get; set; }
    /// <summary>Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</summary>
    public string? OrganizationTenantId { get; set; }
}
