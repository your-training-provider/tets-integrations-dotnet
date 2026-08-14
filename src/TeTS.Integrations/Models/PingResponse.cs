using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Result of the onboarding smoke check: the connection and tenant your API key resolves to.</summary>
public sealed class PingResponse
{
    /// <summary>True when the API key resolved to an active connection.</summary>
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    /// <summary>Your integration's slug, as configured on the connection.</summary>
    [JsonPropertyName("integrationSlug")] public string IntegrationSlug { get; set; } = "";
    /// <summary>Unique ID of the resolved connection.</summary>
    [JsonPropertyName("connectionId")] public string ConnectionId { get; set; } = "";
    /// <summary>Human-readable label for the resolved connection, as configured by TeTS.</summary>
    [JsonPropertyName("connectionLabel")] public string ConnectionLabel { get; set; } = "";
    /// <summary>Org root group UUID; send as the tenant ID on scoped requests.</summary>
    [JsonPropertyName("organizationTenantId")] public string OrganizationTenantId { get; set; } = "";
    /// <summary>Unique ID of the resolved organization.</summary>
    [JsonPropertyName("orgId")] public string OrgId { get; set; } = "";
    /// <summary>Server-generated request ID for this ping — include it in support requests.</summary>
    [JsonPropertyName("requestId")] public string RequestId { get; set; } = "";
}
