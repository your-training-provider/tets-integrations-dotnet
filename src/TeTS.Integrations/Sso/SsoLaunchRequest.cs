namespace TeTS.Integrations.Sso;

/// <summary>
/// Parameters for a signed SSO launch URL. Only <see cref="UserName"/> is required.
/// The full legacy (Topyx-compatible) surface is supported: JIT profile fields,
/// course/program targets, and iframe embed.
/// </summary>
public sealed class SsoLaunchRequest
{
    /// <summary>Platform username to sign in as. Required. ASCII only (signature constraint).</summary>
    public string UserName { get; set; } = "";
    /// <summary>Session duration in seconds, 1..28800 (8 h max). Default 28800.</summary>
    public int SessionTimeOutSeconds { get; set; } = 28800;
    /// <summary>Your stable staff identifier; upserted as the user's externalId.</summary>
    public string? Identification { get; set; }
    /// <summary>JIT-provisioning profile field: the learner's first name.</summary>
    public string? FirstName { get; set; }
    /// <summary>JIT-provisioning profile field: the learner's last name.</summary>
    public string? LastName { get; set; }
    /// <summary>JIT-provisioning profile field: the learner's email address.</summary>
    public string? Email { get; set; }
    /// <summary>JIT-provisioning profile field: the learner's organization/company name.</summary>
    public string? Organization { get; set; }
    /// <summary>JIT-provisioning profile field: the learner's job title.</summary>
    public string? JobTitle { get; set; }
    /// <summary>Legacy numeric course ID to launch directly into.</summary>
    public string? CourseId { get; set; }
    /// <summary>Display name of the course identified by <see cref="CourseId"/>, for JIT provisioning.</summary>
    public string? CourseName { get; set; }
    /// <summary>Legacy numeric content ID within the course to launch directly into.</summary>
    public string? ContentId { get; set; }
    /// <summary>Legacy numeric program ID to launch directly into.</summary>
    public string? ProgramId { get; set; }
    /// <summary>Display name of the program identified by <see cref="ProgramId"/>, for JIT provisioning.</summary>
    public string? ProgramName { get; set; }
    /// <summary>Org root group UUID. Required when your integration serves multiple organizations.</summary>
    public string? OrganizationTenantId { get; set; }
    /// <summary>Request iframe-native hosted course launch (course targets only).</summary>
    public bool Embed { get; set; }
    /// <summary>Exact parent page origin approved to frame the player, e.g. https://app.partner.example.</summary>
    public string? EmbedOrigin { get; set; }
    /// <summary>Test hook: fixed unix-seconds timestamp instead of "now". Leave null in production.</summary>
    public long? TimestampOverride { get; set; }
}
