using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Result of <see cref="TeTS.Integrations.Resources.UsersResource.LinkAsync"/>.</summary>
public sealed class LinkUserResult
{
    /// <summary>The linked user, now carrying your <c>ExternalId</c>.</summary>
    [JsonPropertyName("user")] public User User { get; set; } = null!;
    /// <summary>
    /// True when this call created the link; false when the identical link already existed
    /// (the call is idempotent, so a retry after a lost response is safe).
    /// </summary>
    [JsonPropertyName("created")] public bool Created { get; set; }
}
