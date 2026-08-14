namespace TeTS.Integrations;

/// <summary>Configuration for <see cref="TetsIntegrationsClient"/>.</summary>
public sealed class TetsOptions
{
    /// <summary>Platform base URL, e.g. https://courses.example.com (no trailing slash needed).</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>API key issued by TeTS; sent as the x-api-key header on every request.</summary>
    public string ApiKey { get; set; } = "";
    /// <summary>Org root group UUID. Required for unscoped keys on multi-org integrations; harmless otherwise.</summary>
    public string? OrganizationTenantId { get; set; }
    /// <summary>Integration slug for SSO launch URLs (e.g. your assigned slug). Needed only for <c>Sso</c>.</summary>
    public string? IntegrationSlug { get; set; }
    /// <summary>Shared SSO secret issued by TeTS. Needed only for <c>Sso</c>.</summary>
    public string? SsoSecret { get; set; }
    /// <summary>Retries after a failed attempt (429/5xx/transport). 0 disables. Default 3.</summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>Request timeout applied when the SDK owns the HttpClient. Default 30 s.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
